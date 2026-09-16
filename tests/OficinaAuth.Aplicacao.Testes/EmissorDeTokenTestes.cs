using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao.Testes;

public class EmissorDeTokenTestes
{
    // Mesmo valor de GeradorTokenDeTeste.Secret no oficina-app: deixa explícito que os
    // dois lados assinam/validam com um único segredo compartilhado.
    private const string Segredo = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";

    private readonly RelogioFixo _relogio = new();
    private readonly Mock<IProvedorDeSegredos> _segredos = new();

    public EmissorDeTokenTestes()
    {
        _segredos.Setup(s => s.ObterAsync(NomesDeSegredos.Jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Segredo);
    }

    private EmissorDeToken Sut() => new(_segredos.Object, _relogio);

    /// <summary>
    /// CÓPIA dos TokenValidationParameters de oficina-app/src/Oficina.Api/Configuracao/ConfiguracaoJwt.cs.
    /// Se este teste passa, a API aceita o token. Se a API mudar a validação, atualize aqui.
    /// </summary>
    private static TokenValidationParameters ParametrosDeValidacaoDaApi() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "oficina-auth",
        ValidateAudience = true,
        ValidAudience = "oficina-api",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Segredo)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    [Fact]
    public async Task Emitir_TokenDeCliente_DeveSerAceitoPelaValidacaoDaApi()
    {
        var sub = Guid.NewGuid();
        var emitido = await Sut().EmitirAsync(sub, Perfis.Cliente, "Joao", "39053344705", default);

        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        var resultado = await handler.ValidateTokenAsync(emitido.AccessToken, ParametrosDeValidacaoDaApi());

        resultado.IsValid.Should().BeTrue(resultado.Exception?.ToString());
        resultado.Claims["sub"].Should().Be(sub.ToString());
        resultado.Claims["perfil"].Should().Be("Cliente");
        resultado.Claims["documento"].Should().Be("39053344705");
        resultado.Claims["nome"].Should().Be("Joao");
        resultado.Claims.Should().ContainKey("jti");
        Guid.TryParse(resultado.Claims["jti"].ToString(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Emitir_DeveUsarHs256EExpirarEm60Minutos()
    {
        var emitido = await Sut().EmitirAsync(Guid.NewGuid(), Perfis.Admin, "admin", null, default);
        var jwt = new JsonWebToken(emitido.AccessToken);

        jwt.Alg.Should().Be("HS256");
        jwt.Issuer.Should().Be("oficina-auth");
        jwt.Audiences.Should().ContainSingle().Which.Should().Be("oficina-api");
        jwt.ValidTo.Should().Be(_relogio.Agora.UtcDateTime.AddMinutes(60));
        jwt.IssuedAt.Should().Be(_relogio.Agora.UtcDateTime);
        emitido.ExpiresIn.Should().Be(3600);
        emitido.TokenType.Should().Be("Bearer");
        emitido.Perfil.Should().Be("Admin");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Atendente")]
    public async Task Emitir_TokenAdministrativo_NaoDeveTerClaimDocumento(string perfil)
    {
        var emitido = await Sut().EmitirAsync(Guid.NewGuid(), perfil, "admin", null, default);
        var jwt = new JsonWebToken(emitido.AccessToken);

        jwt.TryGetPayloadValue<string>("documento", out _).Should().BeFalse();
        jwt.GetPayloadValue<string>("perfil").Should().Be(perfil);
    }

    [Fact]
    public async Task Emitir_ClienteSemDocumento_DeveRecusar()
    {
        var act = () => Sut().EmitirAsync(Guid.NewGuid(), Perfis.Cliente, "Joao", null, default);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*documento*");
    }

    [Fact]
    public async Task Emitir_PerfilAdministrativoComDocumento_DeveRecusar()
    {
        var act = () => Sut().EmitirAsync(Guid.NewGuid(), Perfis.Admin, "admin", "39053344705", default);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*documento*");
    }

    [Fact]
    public async Task Emitir_PerfilForaDoVocabulario_DeveRecusar()
    {
        var act = () => Sut().EmitirAsync(Guid.NewGuid(), "Gerente", "x", null, default);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*perfil*");
    }

    [Fact]
    public async Task Emitir_ComSegredoCurto_DeveFalharAlto()
    {
        _segredos.Setup(s => s.ObterAsync(NomesDeSegredos.Jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync("curto");

        var act = () => Sut().EmitirAsync(Guid.NewGuid(), Perfis.Admin, "admin", null, default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*32*");
    }

    [Fact]
    public async Task Emitir_TokenComSegredoDiferente_DeveSerRejeitadoPelaApi()
    {
        _segredos.Setup(s => s.ObterAsync(NomesDeSegredos.Jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync("outro-segredo-com-mais-de-32-caracteres-xxxxxxxx");

        var emitido = await Sut().EmitirAsync(Guid.NewGuid(), Perfis.Admin, "admin", null, default);
        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(emitido.AccessToken, ParametrosDeValidacaoDaApi());

        resultado.IsValid.Should().BeFalse();
    }
}
