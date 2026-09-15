using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SecretsManager;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace OficinaAuth.Authorizer;

/// <summary>
/// Lambda Authorizer do API Gateway (HTTP API, payload 2.0, enable_simple_responses).
/// Handler: <c>OficinaAuth.Authorizer::OficinaAuth.Authorizer.Function::HandlerAsync</c>.
/// O gateway cacheia a resposta por 300s por valor do header Authorization.
/// </summary>
public class Function
{
    private readonly LeitorDeSegredo _segredo;

    /// <summary>Construtor usado pelo runtime da Lambda.</summary>
    public Function() : this(new LeitorDeSegredo(new AmazonSecretsManagerClient())) { }

    public Function(LeitorDeSegredo segredo) => _segredo = segredo;

    public async Task<APIGatewayCustomAuthorizerV2SimpleResponse> HandlerAsync(
        APIGatewayCustomAuthorizerV2Request requisicao, ILambdaContext contexto)
    {
        var header = requisicao.Headers?
            .FirstOrDefault(h => string.Equals(h.Key, "authorization", StringComparison.OrdinalIgnoreCase))
            .Value;

        var segredo = await _segredo.ObterJwtAsync(CancellationToken.None);
        var resultado = await ValidadorDeToken.Validar(header, segredo);

        if (!resultado.Valido)
        {
            // Sem o token em si no log: só o motivo e a rota.
            contexto.Logger?.LogLine($"NEGADO rota={requisicao.RouteArn} motivo={resultado.Motivo} requestId={contexto.AwsRequestId}");
            return new APIGatewayCustomAuthorizerV2SimpleResponse { IsAuthorized = false };
        }

        return new APIGatewayCustomAuthorizerV2SimpleResponse
        {
            IsAuthorized = true,
            Context = resultado.Contexto.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
        };
    }
}
