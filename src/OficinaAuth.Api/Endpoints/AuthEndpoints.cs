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
    /// requestContext.http.sourceIp do API Gateway. Fora dela (TestServer) vem nulo.
    /// </summary>
    private static string Origem(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "desconhecida";
}
