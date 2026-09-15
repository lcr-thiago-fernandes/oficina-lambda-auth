using FluentAssertions;
using Moq;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Infraestrutura.Persistencia;

namespace OficinaAuth.Infraestrutura.Testes;

/// <summary>
/// Não precisa de banco: <see cref="Npgsql.NpgsqlDataSource.Create(string)"/> só monta o
/// pool, não abre conexão. Cobre o construtor que resolve a senha via
/// <see cref="IProvedorDeSegredos"/>, sem depender do Testcontainers.
/// </summary>
public class FabricaDeConexaoTestes
{
    private static readonly ParametrosDoBanco Parametros = new("localhost", 5432, "oficina", "usuario");

    [Fact]
    public async Task ObterAsync_QuandoConstrucaoFalha_NaoCacheiaFalhaEConsegueNaProximaChamada()
    {
        var segredos = new Mock<IProvedorDeSegredos>();
        segredos.SetupSequence(s => s.ObterAsync(NomesDeSegredos.SenhaDoBanco, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Secrets Manager indisponível"))
            .ReturnsAsync("senha-qualquer");

        var fabrica = new FabricaDeConexao(Parametros, segredos.Object);

        Func<Task> primeiraChamada = async () => await fabrica.ObterAsync(default);
        await primeiraChamada.Should().ThrowAsync<InvalidOperationException>();

        var dataSource = await fabrica.ObterAsync(default);
        dataSource.Should().NotBeNull();

        segredos.Verify(
            s => s.ObterAsync(NomesDeSegredos.SenhaDoBanco, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ObterAsync_AposSucesso_DeveReutilizarOMesmoDataSource()
    {
        var segredos = new Mock<IProvedorDeSegredos>();
        segredos.Setup(s => s.ObterAsync(NomesDeSegredos.SenhaDoBanco, It.IsAny<CancellationToken>()))
            .ReturnsAsync("senha-qualquer");

        var fabrica = new FabricaDeConexao(Parametros, segredos.Object);

        var a = await fabrica.ObterAsync(default);
        var b = await fabrica.ObterAsync(default);

        a.Should().BeSameAs(b);
        segredos.Verify(
            s => s.ObterAsync(NomesDeSegredos.SenhaDoBanco, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ObterAsync_QuandoOChamadorCancelaComAConstrucaoAindaRodando_NaoDescartaAConstrucaoCompartilhada()
    {
        var senhaPendente = new TaskCompletionSource<string>();
        var segredos = new Mock<IProvedorDeSegredos>();
        segredos.Setup(s => s.ObterAsync(NomesDeSegredos.SenhaDoBanco, It.IsAny<CancellationToken>()))
            .Returns(senhaPendente.Task);

        var fabrica = new FabricaDeConexao(Parametros, segredos.Object);

        // ct já cancelado: o chamador desiste de esperar, mas a construção
        // compartilhada (ainda presa no ObterAsync do provedor) continua rodando.
        Func<Task> chamadaCancelada = async () => await fabrica.ObterAsync(new CancellationToken(canceled: true)).AsTask();
        await chamadaCancelada.Should().ThrowAsync<OperationCanceledException>();

        senhaPendente.SetResult("senha-qualquer");

        var dataSource = await fabrica.ObterAsync(CancellationToken.None);
        dataSource.Should().NotBeNull();

        // Só uma construção aconteceu: a chamada cancelada não descartou a
        // construção em andamento nem disparou uma segunda em paralelo.
        segredos.Verify(
            s => s.ObterAsync(NomesDeSegredos.SenhaDoBanco, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
