using FluentAssertions;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Infraestrutura.Tentativas;

namespace OficinaAuth.Infraestrutura.Testes;

public class LimitadorDeTentativasMemoriaTestes
{
    private sealed class RelogioFixo : IRelogio
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly RelogioFixo _relogio = new();
    private readonly TimeSpan _janela = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task ContarFalhas_SemRegistro_DeveSerZero()
    {
        var sut = new LimitadorDeTentativasMemoria(_relogio);
        (await sut.ContarFalhasAsync("origem:x", default)).Should().Be(0);
    }

    [Fact]
    public async Task RegistrarFalha_DeveAcumularDentroDaJanela()
    {
        var sut = new LimitadorDeTentativasMemoria(_relogio);
        await sut.RegistrarFalhaAsync("origem:x", _janela, default);
        await sut.RegistrarFalhaAsync("origem:x", _janela, default);

        (await sut.ContarFalhasAsync("origem:x", default)).Should().Be(2);
    }

    [Fact]
    public async Task ContarFalhas_DepoisDaJanela_DeveZerar()
    {
        var sut = new LimitadorDeTentativasMemoria(_relogio);
        await sut.RegistrarFalhaAsync("origem:x", _janela, default);

        _relogio.Agora = _relogio.Agora.AddMinutes(16);

        (await sut.ContarFalhasAsync("origem:x", default)).Should().Be(0);
    }

    [Fact]
    public async Task Janela_ComecaNaPrimeiraFalhaENaoSeEstende()
    {
        var sut = new LimitadorDeTentativasMemoria(_relogio);
        await sut.RegistrarFalhaAsync("origem:x", _janela, default);
        _relogio.Agora = _relogio.Agora.AddMinutes(14);
        await sut.RegistrarFalhaAsync("origem:x", _janela, default);
        _relogio.Agora = _relogio.Agora.AddMinutes(2);

        (await sut.ContarFalhasAsync("origem:x", default)).Should().Be(0);
    }

    [Fact]
    public async Task Limpar_DeveRemoverAChave()
    {
        var sut = new LimitadorDeTentativasMemoria(_relogio);
        await sut.RegistrarFalhaAsync("identidade:admin", _janela, default);
        await sut.LimparAsync("identidade:admin", default);

        (await sut.ContarFalhasAsync("identidade:admin", default)).Should().Be(0);
    }
}
