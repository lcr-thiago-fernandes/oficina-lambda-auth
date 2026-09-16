using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Moq;
using OficinaAuth.Infraestrutura.Segredos;

namespace OficinaAuth.Infraestrutura.Testes;

[Collection("SecretsManagerCacheEstatico")]
public class ProvedorDeSegredosSecretsManagerTestes
{
    private readonly Mock<IAmazonSecretsManager> _sm = new();

    public ProvedorDeSegredosSecretsManagerTestes() => ProvedorDeSegredosSecretsManager.LimparCacheParaTestes();

    private void SegredoNaAws(string nome, string valor) =>
        _sm.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == nome), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new GetSecretValueResponse { SecretString = valor });

    [Fact]
    public async Task Obter_DeveLerSecretStringComoTextoPuro()
    {
        SegredoNaAws("oficina/jwt_secret", "um-segredo-em-texto-puro-com-mais-de-32-chars");

        var valor = await new ProvedorDeSegredosSecretsManager(_sm.Object).ObterAsync("oficina/jwt_secret", default);

        valor.Should().Be("um-segredo-em-texto-puro-com-mais-de-32-chars");
    }

    [Fact]
    public async Task Obter_DuasVezes_DeveChamarAAwsUmaSoVez()
    {
        SegredoNaAws("oficina/jwt_secret", "valor");
        var sut = new ProvedorDeSegredosSecretsManager(_sm.Object);

        await sut.ObterAsync("oficina/jwt_secret", default);
        await sut.ObterAsync("oficina/jwt_secret", default);

        _sm.Verify(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cache_DeveSobreviverAInstanciasDiferentes()
    {
        // Em Lambda a instância pode ser recriada pelo DI; o cache é estático de propósito.
        SegredoNaAws("oficina/db_password", "senha");

        await new ProvedorDeSegredosSecretsManager(_sm.Object).ObterAsync("oficina/db_password", default);
        await new ProvedorDeSegredosSecretsManager(_sm.Object).ObterAsync("oficina/db_password", default);

        _sm.Verify(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Obter_ValorGravadoComoJson_DeveFalharAltoExplicandoOContrato()
    {
        // Padrão do console da AWS é gravar {"chave":"valor"}; o contrato exige texto puro.
        SegredoNaAws("oficina/jwt_secret", """{"jwt_secret":"abc"}""");

        var act = () => new ProvedorDeSegredosSecretsManager(_sm.Object).ObterAsync("oficina/jwt_secret", default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*texto puro*");
    }

    [Fact]
    public async Task Obter_ValorVazio_DeveFalhar()
    {
        SegredoNaAws("oficina/jwt_secret", "");

        var act = () => new ProvedorDeSegredosSecretsManager(_sm.Object).ObterAsync("oficina/jwt_secret", default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*vazio*");
    }

    [Fact]
    public async Task Falha_NaoDeveFicarNoCache()
    {
        var chamadas = 0;
        _sm.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(() => ++chamadas == 1
               ? throw new ResourceNotFoundException("ainda nao existe")
               : new GetSecretValueResponse { SecretString = "agora-sim" });
        var sut = new ProvedorDeSegredosSecretsManager(_sm.Object);

        await sut.Invoking(s => s.ObterAsync("oficina/jwt_secret", default)).Should().ThrowAsync<ResourceNotFoundException>();
        (await sut.ObterAsync("oficina/jwt_secret", default)).Should().Be("agora-sim");
    }

    [Fact]
    public async Task Obter_CancelamentoDoChamador_NaoDescartaATarefaCompartilhada()
    {
        // Só o ct do CHAMADOR cancela (via WaitAsync(ct)); a tarefa compartilhada de
        // busca no Secrets Manager continua rodando e não pode ser evictada do cache,
        // senão a próxima chamada dispararia uma busca concorrente duplicada.
        var tcs = new TaskCompletionSource<GetSecretValueResponse>();
        _sm.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
           .Returns(tcs.Task);
        var sut = new ProvedorDeSegredosSecretsManager(_sm.Object);
        using var cts = new CancellationTokenSource();

        var chamada = sut.ObterAsync("oficina/jwt_secret", cts.Token);
        cts.Cancel();
        await sut.Invoking(_ => chamada).Should().ThrowAsync<OperationCanceledException>();

        tcs.SetResult(new GetSecretValueResponse { SecretString = "valor-compartilhado" });

        (await sut.ObterAsync("oficina/jwt_secret", CancellationToken.None)).Should().Be("valor-compartilhado");
        _sm.Verify(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
