using FluentAssertions;
using Moq;
using OficinaAuth.Aplicacao;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tentativas;
using OficinaAuth.Aplicacao.Tokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao.Testes;

public class AutenticarClienteUseCaseTestes
{
    private const string Segredo = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";
    private const string Origem = "203.0.113.10";

    private readonly Mock<IClienteRepositorio> _clientes = new();
    private readonly Mock<ILimitadorDeTentativas> _limitador = new();
    private readonly Mock<IProvedorDeSegredos> _segredos = new();

    public AutenticarClienteUseCaseTestes()
    {
        _segredos.Setup(s => s.ObterAsync(NomesDeSegredos.Jwt, It.IsAny<CancellationToken>())).ReturnsAsync(Segredo);
        _limitador.Setup(l => l.ContarFalhasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    private AutenticarClienteUseCase Sut() => new(
        _clientes.Object,
        new EmissorDeToken(_segredos.Object, new RelogioFixo()),
        new ProtecaoContraForcaBruta(_limitador.Object, PoliticaDeTentativas.Padrao));

    private void ClienteNoBanco(ClienteAutenticavel? cliente, string documento = "39053344705") =>
        _clientes.Setup(c => c.ObterPorDocumentoAsync(documento, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

    [Fact]
    public async Task ClienteAtivo_DeveEmitirTokenDeClienteComDocumento()
    {
        var id = Guid.NewGuid();
        ClienteNoBanco(new ClienteAutenticavel(id, "Joao", "39053344705", Ativo: true));

        var token = await Sut().ExecutarAsync(new AutenticarClienteRequest("390.533.447-05"), Origem, default);

        token.Perfil.Should().Be(Perfis.Cliente);
        token.ExpiresIn.Should().Be(3600);
        var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token.AccessToken);
        jwt.Subject.Should().Be(id.ToString());
        jwt.GetPayloadValue<string>("documento").Should().Be("39053344705");
        jwt.GetPayloadValue<string>("nome").Should().Be("Joao");
    }

    [Fact]
    public async Task CpfComMascara_DeveConsultarORepositorioSoComDigitos()
    {
        ClienteNoBanco(new ClienteAutenticavel(Guid.NewGuid(), "Joao", "39053344705", true));

        await Sut().ExecutarAsync(new AutenticarClienteRequest("390.533.447-05"), Origem, default);

        _clientes.Verify(c => c.ObterPorDocumentoAsync("39053344705", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("11111111111")]
    [InlineData("12345678900")]
    [InlineData("abc")]
    public async Task CpfInvalido_DeveLancarDocumentoInvalidoSemConsultarBanco(string? cpf)
    {
        var act = () => Sut().ExecutarAsync(new AutenticarClienteRequest(cpf), Origem, default);

        await act.Should().ThrowAsync<DocumentoInvalidoException>();
        _clientes.Verify(c => c.ObterPorDocumentoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClienteInexistente_DeveLancarNaoEncontradoERegistrarFalhaNaOrigem()
    {
        ClienteNoBanco(null);

        var act = () => Sut().ExecutarAsync(new AutenticarClienteRequest("39053344705"), Origem, default);

        await act.Should().ThrowAsync<ClienteNaoEncontradoException>();
        _limitador.Verify(l => l.RegistrarFalhaAsync($"origem:{Origem}", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClienteInativo_DeveLancarInativoERegistrarFalha()
    {
        ClienteNoBanco(new ClienteAutenticavel(Guid.NewGuid(), "Joao", "39053344705", Ativo: false));

        var act = () => Sut().ExecutarAsync(new AutenticarClienteRequest("39053344705"), Origem, default);

        await act.Should().ThrowAsync<ClienteInativoException>();
        _limitador.Verify(l => l.RegistrarFalhaAsync($"origem:{Origem}", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OrigemBloqueada_DeveLancar429AntesDeConsultarBanco()
    {
        _limitador.Setup(l => l.ContarFalhasAsync($"origem:{Origem}", It.IsAny<CancellationToken>())).ReturnsAsync(10);

        var act = () => Sut().ExecutarAsync(new AutenticarClienteRequest("39053344705"), Origem, default);

        await act.Should().ThrowAsync<MuitasTentativasException>();
        _clientes.Verify(c => c.ObterPorDocumentoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sucesso_NaoRegistraFalhaNemLimpaOrigem()
    {
        ClienteNoBanco(new ClienteAutenticavel(Guid.NewGuid(), "Joao", "39053344705", true));

        await Sut().ExecutarAsync(new AutenticarClienteRequest("39053344705"), Origem, default);

        _limitador.Verify(l => l.RegistrarFalhaAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _limitador.Verify(l => l.LimparAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
