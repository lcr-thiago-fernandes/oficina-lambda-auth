using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using OficinaAuth.Aplicacao;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tentativas;
using OficinaAuth.Aplicacao.Tokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao.Testes;

public class AutenticarAdminUseCaseTestes
{
    private const string Segredo = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";
    private const string Origem = "203.0.113.10";
    // Custo 4 só para os testes correrem rápido; Verify aceita qualquer custo.
    private static readonly string HashDeAlteraMe = BCrypt.Net.BCrypt.HashPassword("AlteraMe@123", 4);

    private readonly Mock<IUsuarioRepositorio> _usuarios = new();
    private readonly Mock<ILimitadorDeTentativas> _limitador = new();
    private readonly Mock<IProvedorDeSegredos> _segredos = new();

    public AutenticarAdminUseCaseTestes()
    {
        _segredos.Setup(s => s.ObterAsync(NomesDeSegredos.Jwt, It.IsAny<CancellationToken>())).ReturnsAsync(Segredo);
        _limitador.Setup(l => l.ContarFalhasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    private AutenticarAdminUseCase Sut() => new(
        _usuarios.Object,
        new EmissorDeToken(_segredos.Object, new RelogioFixo()),
        new ProtecaoContraForcaBruta(_limitador.Object, PoliticaDeTentativas.Padrao));

    private UsuarioAutenticavel Admin(bool ativo = true, string perfil = "Admin") =>
        new(Guid.NewGuid(), "admin", HashDeAlteraMe, perfil, ativo);

    private void UsuarioNoBanco(UsuarioAutenticavel? u, string username = "admin") =>
        _usuarios.Setup(r => r.ObterPorUsernameAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync(u);

    [Theory]
    [InlineData("Admin")]
    [InlineData("Atendente")]
    public async Task CredenciaisCorretas_DeveEmitirTokenComPerfilDoBancoSemDocumento(string perfil)
    {
        var usuario = Admin(perfil: perfil);
        UsuarioNoBanco(usuario);

        var token = await Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "AlteraMe@123"), Origem, default);

        token.Perfil.Should().Be(perfil);
        var jwt = new JsonWebToken(token.AccessToken);
        jwt.Subject.Should().Be(usuario.Id.ToString());
        jwt.GetPayloadValue<string>("perfil").Should().Be(perfil);
        jwt.GetPayloadValue<string>("nome").Should().Be("admin");
        jwt.TryGetPayloadValue<string>("documento", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Username_DeveSerNormalizadoAntesDaConsulta()
    {
        UsuarioNoBanco(Admin());

        await Sut().ExecutarAsync(new AutenticarAdminRequest("  ADMIN ", "AlteraMe@123"), Origem, default);

        _usuarios.Verify(r => r.ObterPorUsernameAsync("admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "senha")]
    [InlineData("", "senha")]
    [InlineData("admin", null)]
    [InlineData("admin", "")]
    public async Task CamposVazios_DeveLancarArgumentExceptionSemConsultarBanco(string? username, string? password)
    {
        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest(username, password), Origem, default);

        await act.Should().ThrowAsync<ArgumentException>();
        _usuarios.Verify(r => r.ObterPorUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UsernameComFormatoInvalido_DeveResponderComoCredenciaisInvalidas()
    {
        // Formato inválido (ex.: com espaço) não revela a regra de formato: é só 401.
        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("nome com espaco", "x"), Origem, default);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
        _usuarios.Verify(r => r.ObterPorUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UsuarioInexistente_DeveLancarCredenciaisInvalidasERegistrarFalhaEmOrigemEIdentidade()
    {
        UsuarioNoBanco(null, "fantasma");

        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("fantasma", "x"), Origem, default);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
        _limitador.Verify(l => l.RegistrarFalhaAsync($"origem:{Origem}", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _limitador.Verify(l => l.RegistrarFalhaAsync("identidade:fantasma", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SenhaErrada_DeveLancarCredenciaisInvalidasERegistrarFalha()
    {
        UsuarioNoBanco(Admin());

        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "errada"), Origem, default);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
        _limitador.Verify(l => l.RegistrarFalhaAsync("identidade:admin", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UsuarioInativoComSenhaCorreta_DeveLancarInativoSemRegistrarFalha()
    {
        UsuarioNoBanco(Admin(ativo: false));

        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "AlteraMe@123"), Origem, default);

        await act.Should().ThrowAsync<UsuarioInativoException>();
        _limitador.Verify(l => l.RegistrarFalhaAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UsuarioInativoComSenhaErrada_DeveResponder401ENao403()
    {
        // Inatividade só é revelada a quem tem a senha certa.
        UsuarioNoBanco(Admin(ativo: false));

        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "errada"), Origem, default);

        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task PerfilForaDoVocabulario_DeveLancarPerfilDesconhecido()
    {
        UsuarioNoBanco(Admin(perfil: "Gerente"));

        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "AlteraMe@123"), Origem, default);

        await act.Should().ThrowAsync<PerfilDesconhecidoException>();
    }

    [Fact]
    public async Task IdentidadeBloqueada_DeveLancar429AntesDeConsultarBanco()
    {
        _limitador.Setup(l => l.ContarFalhasAsync("identidade:admin", It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = () => Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "AlteraMe@123"), Origem, default);

        await act.Should().ThrowAsync<MuitasTentativasException>();
        _usuarios.Verify(r => r.ObterPorUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sucesso_DeveLimparAIdentidade()
    {
        UsuarioNoBanco(Admin());

        await Sut().ExecutarAsync(new AutenticarAdminRequest("admin", "AlteraMe@123"), Origem, default);

        _limitador.Verify(l => l.LimparAsync("identidade:admin", It.IsAny<CancellationToken>()), Times.Once);
    }
}
