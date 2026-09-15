using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao.Tokens;

public sealed class EmissorDeToken
{
    private readonly IProvedorDeSegredos _segredos;
    private readonly IRelogio _relogio;

    public EmissorDeToken(IProvedorDeSegredos segredos, IRelogio relogio)
    {
        _segredos = segredos;
        _relogio = relogio;
    }

    public async Task<TokenEmitido> EmitirAsync(Guid sub, string perfil, string nome, string? documento, CancellationToken ct)
    {
        if (perfil is not (Perfis.Cliente or Perfis.Atendente or Perfis.Admin))
            throw new ArgumentException($"perfil '{perfil}' fora do vocabulário (Cliente, Atendente, Admin).", nameof(perfil));

        if (perfil == Perfis.Cliente && string.IsNullOrWhiteSpace(documento))
            throw new ArgumentException("Token de Cliente exige a claim documento.", nameof(documento));

        if (perfil != Perfis.Cliente && documento is not null)
            throw new ArgumentException("Token administrativo não pode carregar a claim documento.", nameof(documento));

        var segredo = await _segredos.ObterAsync(NomesDeSegredos.Jwt, ct);
        if (string.IsNullOrWhiteSpace(segredo) || segredo.Length < ContratoDoToken.TamanhoMinimoDoSegredo)
            throw new InvalidOperationException(
                $"O segredo '{NomesDeSegredos.Jwt}' precisa ter pelo menos {ContratoDoToken.TamanhoMinimoDoSegredo} caracteres.");

        var agora = _relogio.Agora.UtcDateTime;

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = sub.ToString(),
            [ContratoDoToken.ClaimPerfil] = perfil,
            [ContratoDoToken.ClaimNome] = nome,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };
        if (documento is not null)
            claims[ContratoDoToken.ClaimDocumento] = documento;

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = ContratoDoToken.Emissor,
            Audience = ContratoDoToken.Audiencia,
            Claims = claims,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora.Add(ContratoDoToken.Validade),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var token = handler.CreateToken(descritor);

        return new TokenEmitido(token, "Bearer", (int)ContratoDoToken.Validade.TotalSeconds, perfil);
    }
}
