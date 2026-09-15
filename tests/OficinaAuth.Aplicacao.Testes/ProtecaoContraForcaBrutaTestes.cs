using FluentAssertions;
using Moq;
using OficinaAuth.Aplicacao;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tentativas;

namespace OficinaAuth.Aplicacao.Testes;

public class ProtecaoContraForcaBrutaTestes
{
    private readonly Mock<ILimitadorDeTentativas> _limitador = new();
    private readonly PoliticaDeTentativas _politica = new(MaximoPorOrigem: 3, MaximoPorIdentidade: 2, Janela: TimeSpan.FromMinutes(15));

    private ProtecaoContraForcaBruta Sut() => new(_limitador.Object, _politica);

    private void FalhasRegistradas(string chave, int quantidade) =>
        _limitador.Setup(l => l.ContarFalhasAsync(chave, It.IsAny<CancellationToken>())).ReturnsAsync(quantidade);

    [Fact]
    public async Task ExigirPermitido_AbaixoDosLimites_NaoLanca()
    {
        FalhasRegistradas("origem:1.2.3.4", 2);
        FalhasRegistradas("identidade:admin", 1);

        await Sut().ExigirPermitidoAsync("1.2.3.4", "admin", default);
    }

    [Fact]
    public async Task ExigirPermitido_OrigemNoLimite_Lanca429ComRetryAfterDaJanela()
    {
        FalhasRegistradas("origem:1.2.3.4", 3);

        var act = () => Sut().ExigirPermitidoAsync("1.2.3.4", null, default);

        (await act.Should().ThrowAsync<MuitasTentativasException>())
            .Which.RetryAfter.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task ExigirPermitido_IdentidadeNoLimite_LancaMesmoComOrigemLivre()
    {
        FalhasRegistradas("origem:9.9.9.9", 0);
        FalhasRegistradas("identidade:admin", 2);

        var act = () => Sut().ExigirPermitidoAsync("9.9.9.9", "admin", default);

        await act.Should().ThrowAsync<MuitasTentativasException>();
    }

    [Fact]
    public async Task ExigirPermitido_SemIdentidade_NaoConsultaChaveDeIdentidade()
    {
        FalhasRegistradas("origem:1.2.3.4", 0);

        await Sut().ExigirPermitidoAsync("1.2.3.4", null, default);

        _limitador.Verify(l => l.ContarFalhasAsync(It.Is<string>(c => c.StartsWith("identidade:")), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExigirPermitido_SemOrigem_NaoConsultaChaveDeOrigem()
    {
        await Sut().ExigirPermitidoAsync(null, "admin", default);

        _limitador.Verify(l => l.ContarFalhasAsync(It.Is<string>(c => c.StartsWith("origem:")), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarFalha_SemOrigem_NaoRegistraChaveDeOrigem()
    {
        await Sut().RegistrarFalhaAsync(null, "admin", default);

        _limitador.Verify(l => l.RegistrarFalhaAsync(It.Is<string>(c => c.StartsWith("origem:")), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _limitador.Verify(l => l.RegistrarFalhaAsync("identidade:admin", TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarFalha_DeveIncrementarOrigemEIdentidadeComAJanelaDaPolitica()
    {
        await Sut().RegistrarFalhaAsync("1.2.3.4", "Admin", default);

        _limitador.Verify(l => l.RegistrarFalhaAsync("origem:1.2.3.4", TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
        _limitador.Verify(l => l.RegistrarFalhaAsync("identidade:admin", TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarSucesso_DeveLimparSoAIdentidade()
    {
        await Sut().RegistrarSucessoAsync("1.2.3.4", "admin", default);

        _limitador.Verify(l => l.LimparAsync("identidade:admin", It.IsAny<CancellationToken>()), Times.Once);
        _limitador.Verify(l => l.LimparAsync("origem:1.2.3.4", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PoliticaPadrao_DeveSer10PorOrigem5PorIdentidadeEm15Minutos()
    {
        PoliticaDeTentativas.Padrao.Should().Be(new PoliticaDeTentativas(10, 5, TimeSpan.FromMinutes(15)));
    }
}
