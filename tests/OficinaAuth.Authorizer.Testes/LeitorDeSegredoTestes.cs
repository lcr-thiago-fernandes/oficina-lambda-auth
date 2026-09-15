using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Moq;
using OficinaAuth.Authorizer;

namespace OficinaAuth.Authorizer.Testes;

/// <summary>
/// Cobre a correção em relação ao brief original: um cancelamento do TOKEN DO CHAMADOR
/// (via WaitAsync(ct)) não pode descartar o cache estático — só uma falha real da tarefa
/// de leitura (falha ou cancelamento da PRÓPRIA tarefa) deve evictar o cache.
/// </summary>
public class LeitorDeSegredoTestes
{
    private const string Segredo = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";
    private readonly Mock<IAmazonSecretsManager> _sm = new();

    public LeitorDeSegredoTestes() => LeitorDeSegredo.LimparCacheParaTestes();

    [Fact]
    public async Task CancelamentoDoChamador_NaoDeveDescartarCache()
    {
        var tcs = new TaskCompletionSource<GetSecretValueResponse>();
        _sm.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
           .Returns(tcs.Task);

        var leitor = new LeitorDeSegredo(_sm.Object);
        using var cts = new CancellationTokenSource();

        var tarefa = leitor.ObterJwtAsync(cts.Token);
        cts.Cancel();

        Func<Task> agiu = () => tarefa;
        await agiu.Should().ThrowAsync<OperationCanceledException>();

        // A tarefa de leitura em si não falhou nem foi cancelada — só o token do chamador.
        // O cache deve ter sido preservado, então completá-la agora deve valer para todo mundo.
        tcs.SetResult(new GetSecretValueResponse { SecretString = Segredo });

        var valor = await leitor.ObterJwtAsync(CancellationToken.None);
        valor.Should().Be(Segredo);

        _sm.Verify(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
