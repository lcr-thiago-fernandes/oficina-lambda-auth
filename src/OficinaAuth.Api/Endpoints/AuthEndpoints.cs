using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OficinaAuth.Aplicacao;

namespace OficinaAuth.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/auth");

        grupo.MapPost("/cliente", async (AutenticarClienteRequest req, HttpContext http, AutenticarClienteUseCase uc, CancellationToken ct) =>
            Results.Ok(await uc.ExecutarAsync(req, Origem(http), ct)));

        grupo.MapPost("/admin", async (AutenticarAdminRequest req, HttpContext http, AutenticarAdminUseCase uc, CancellationToken ct) =>
            Results.Ok(await uc.ExecutarAsync(req, Origem(http), ct)));

        return app;
    }

    /// <summary>
    /// Em Lambda, Amazon.Lambda.AspNetCoreServer preenche RemoteIpAddress com
    /// requestContext.http.sourceIp do API Gateway. Fora dela (TestServer) vem nulo — nesse
    /// caso devolvemos null em vez de um valor fixo, para a ProtecaoContraForcaBruta pular
    /// a checagem por origem (senão todo cliente sem IP cairia no mesmo balde global).
    /// </summary>
    private static string? Origem(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString();
        if (ip is null)
        {
            var loggerFactory = http.RequestServices.GetRequiredService<ILoggerFactory>();
            loggerFactory.CreateLogger(typeof(AuthEndpoints).FullName!)
                .LogWarning("RemoteIpAddress ausente: esta requisição será limitada só pela identidade, não pela origem.");
        }
        return ip;
    }
}
