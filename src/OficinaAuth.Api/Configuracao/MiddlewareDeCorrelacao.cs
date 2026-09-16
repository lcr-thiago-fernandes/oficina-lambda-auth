using Amazon.Lambda.Core;

namespace OficinaAuth.Api.Configuracao;

/// <summary>
/// Mesmo header do oficina-app (X-Correlation-Id): lê se vier, senão usa o requestId
/// da Lambda (ou gera um), ecoa na resposta e coloca no escopo de log.
/// </summary>
public sealed class MiddlewareDeCorrelacao
{
    public const string Header = "X-Correlation-Id";

    private readonly RequestDelegate _proximo;
    private readonly ILogger<MiddlewareDeCorrelacao> _log;

    public MiddlewareDeCorrelacao(RequestDelegate proximo, ILogger<MiddlewareDeCorrelacao> log)
    {
        _proximo = proximo;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        var id = contexto.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id))
        {
            id = contexto.Items.TryGetValue("LambdaContext", out var lc) && lc is ILambdaContext lambda
                ? lambda.AwsRequestId
                : Guid.NewGuid().ToString();
        }

        // Adiado para o OnStarting: se o handler seguinte lançar, o UseExceptionHandler
        // chama Response.Clear() antes de escrever o ProblemDetails, o que apagaria um
        // header setado aqui diretamente. OnStarting roda por último, logo antes do
        // primeiro byte sair, então sobrevive a esse Clear().
        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });

        using (_log.BeginScope(new Dictionary<string, object> { ["correlationId"] = id }))
        {
            await _proximo(contexto);
        }
    }
}
