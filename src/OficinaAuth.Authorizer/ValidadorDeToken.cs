using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OficinaAuth.Authorizer;

public sealed record ResultadoDaValidacao(bool Valido, IReadOnlyDictionary<string, string> Contexto, string? Motivo)
{
    public static ResultadoDaValidacao Negado(string motivo) => new(false, new Dictionary<string, string>(), motivo);
}

/// <summary>
/// Validação idêntica à do oficina-app (ConfiguracaoJwt.cs): iss, aud, assinatura HS256,
/// expiração com 30s de tolerância. As constantes são REPETIDAS aqui em vez de
/// referenciar OficinaAuth.Aplicacao de propósito: este projeto precisa ficar sem
/// Npgsql/DynamoDB no pacote, porque roda em toda requisição protegida.
/// </summary>
public static class ValidadorDeToken
{
    public const string Emissor = "oficina-auth";
    public const string Audiencia = "oficina-api";

    private static readonly JsonWebTokenHandler Handler = new() { MapInboundClaims = false };

    public static async Task<ResultadoDaValidacao> Validar(string? headerAuthorization, string segredo)
    {
        if (string.IsNullOrWhiteSpace(headerAuthorization))
            return ResultadoDaValidacao.Negado("Header Authorization ausente.");

        const string prefixo = "Bearer ";
        if (!headerAuthorization.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            return ResultadoDaValidacao.Negado("Esquema não é Bearer.");

        var token = headerAuthorization[prefixo.Length..].Trim();
        if (token.Length == 0)
            return ResultadoDaValidacao.Negado("Token vazio.");

        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Emissor,
            ValidateAudience = true,
            ValidAudience = Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        TokenValidationResult resultado;
        try
        {
            resultado = await Handler.ValidateTokenAsync(token, parametros);
        }
        catch (Exception e)
        {
            return ResultadoDaValidacao.Negado($"Token malformado: {e.GetType().Name}");
        }

        if (!resultado.IsValid)
            return ResultadoDaValidacao.Negado(resultado.Exception?.GetType().Name ?? "Token inválido.");

        if (!resultado.Claims.TryGetValue("perfil", out var perfil) || string.IsNullOrWhiteSpace(perfil?.ToString()))
            return ResultadoDaValidacao.Negado("Token sem claim perfil.");

        var contexto = new Dictionary<string, string> { ["perfil"] = perfil.ToString()! };
        if (resultado.Claims.TryGetValue("sub", out var sub) && sub is not null)
            contexto["sub"] = sub.ToString()!;
        if (resultado.Claims.TryGetValue("documento", out var documento) && documento is not null)
            contexto["documento"] = documento.ToString()!;

        return new ResultadoDaValidacao(true, contexto, null);
    }
}
