using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OficinaAuth.Authorizer;

namespace OficinaAuth.Authorizer.Testes;

public class ValidadorDeTokenTestes
{
    // 74 bytes: precisa ser >= 64 bytes (512 bits) para o teste de HS512 conseguir assinar
    // (Microsoft.IdentityModel.Tokens valida o tamanho mínimo da chave por algoritmo).
    private const string Segredo = "chave-de-teste-com-64-bytes-ou-mais-para-suportar-hs256-e-hs512-nos-testes";

    /// <summary>Gera tokens como a oficina-auth-api emite (mesmo formato de GeradorTokenDeTeste do oficina-app).</summary>
    private static string Token(
        string segredo = Segredo, string iss = "oficina-auth", string aud = "oficina-api",
        string perfil = "Cliente", string? documento = "39053344705", int minutos = 60, string alg = SecurityAlgorithms.HmacSha256)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "11111111-1111-1111-1111-111111111111",
            ["perfil"] = perfil,
            ["nome"] = "Teste",
            ["jti"] = Guid.NewGuid().ToString()
        };
        if (documento is not null) claims["documento"] = documento;

        var agora = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = iss,
            Audience = aud,
            Claims = claims,
            IssuedAt = agora.AddMinutes(minutos < 0 ? minutos - 1 : 0),
            NotBefore = agora.AddMinutes(minutos < 0 ? minutos - 1 : 0),
            Expires = agora.AddMinutes(minutos),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo)), alg)
        });
    }

    [Fact]
    public async Task TokenValidoDeCliente_DeveAutorizarComContextoPerfilSubDocumento()
    {
        var r = await ValidadorDeToken.Validar($"Bearer {Token()}", Segredo);

        r.Valido.Should().BeTrue(r.Motivo);
        r.Contexto["perfil"].Should().Be("Cliente");
        r.Contexto["sub"].Should().Be("11111111-1111-1111-1111-111111111111");
        r.Contexto["documento"].Should().Be("39053344705");
    }

    [Fact]
    public async Task TokenAdministrativo_ContextoNaoTemDocumento()
    {
        var r = await ValidadorDeToken.Validar($"Bearer {Token(perfil: "Admin", documento: null)}", Segredo);

        r.Valido.Should().BeTrue();
        r.Contexto.Should().NotContainKey("documento");
        r.Contexto["perfil"].Should().Be("Admin");
    }

    [Fact]
    public async Task PrefixoBearer_DeveSerCaseInsensitive()
    {
        (await ValidadorDeToken.Validar($"bearer {Token()}", Segredo)).Valido.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    public async Task HeaderAusenteOuSemBearer_DeveNegar(string? header)
    {
        var r = await ValidadorDeToken.Validar(header, Segredo);
        r.Valido.Should().BeFalse();
        r.Contexto.Should().BeEmpty();
    }

    [Fact]
    public async Task AssinaturaComOutroSegredo_DeveNegar()
    {
        var r = await ValidadorDeToken.Validar($"Bearer {Token(segredo: "outro-segredo-com-mais-de-32-caracteres-xxxxxx")}", Segredo);
        r.Valido.Should().BeFalse();
    }

    [Fact]
    public async Task TokenExpirado_DeveNegar()
    {
        var r = await ValidadorDeToken.Validar($"Bearer {Token(minutos: -5)}", Segredo);
        r.Valido.Should().BeFalse();
        // O tipo da exceção do IdentityModel para expiração é SecurityTokenExpiredException,
        // não contém a palavra "Lifetime" — ver Step 5 do brief.
        r.Motivo.Should().Contain("Expired");
    }

    [Fact]
    public async Task EmissorErrado_DeveNegar()
    {
        (await ValidadorDeToken.Validar($"Bearer {Token(iss: "outro")}", Segredo)).Valido.Should().BeFalse();
    }

    [Fact]
    public async Task AudienciaErrada_DeveNegar()
    {
        (await ValidadorDeToken.Validar($"Bearer {Token(aud: "outra-api")}", Segredo)).Valido.Should().BeFalse();
    }

    [Fact]
    public async Task AlgoritmoDiferenteDeHs256_DeveNegar()
    {
        // HS512 com o mesmo segredo: assinatura válida, algoritmo fora do contrato.
        (await ValidadorDeToken.Validar($"Bearer {Token(alg: SecurityAlgorithms.HmacSha512)}", Segredo)).Valido.Should().BeFalse();
    }

    [Fact]
    public async Task TokenMalformado_DeveNegarSemLancar()
    {
        (await ValidadorDeToken.Validar("Bearer nao.e.jwt", Segredo)).Valido.Should().BeFalse();
    }

    [Fact]
    public async Task TokenSemPerfil_DeveNegar()
    {
        var agora = DateTime.UtcNow;
        var semPerfil = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "oficina-auth",
            Audience = "oficina-api",
            Claims = new Dictionary<string, object> { ["sub"] = "x" },
            Expires = agora.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Segredo)), SecurityAlgorithms.HmacSha256)
        });

        (await ValidadorDeToken.Validar($"Bearer {semPerfil}", Segredo)).Valido.Should().BeFalse();
    }
}
