using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Moq;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Infraestrutura.Tentativas;

namespace OficinaAuth.Infraestrutura.Testes;

public class LimitadorDeTentativasDynamoDbTestes
{
    private sealed class RelogioFixo : IRelogio
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    }

    private readonly Mock<IAmazonDynamoDB> _dynamo = new();
    private readonly RelogioFixo _relogio = new();
    private const string Tabela = "oficina-auth-tentativas";

    private LimitadorDeTentativasDynamoDb Sut() => new(_dynamo.Object, Tabela, _relogio);

    private void ItemNaTabela(Dictionary<string, AttributeValue>? item) =>
        _dynamo.Setup(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new GetItemResponse { Item = item });

    [Fact]
    public async Task ContarFalhas_SemItem_DeveSerZero()
    {
        ItemNaTabela(null);
        (await Sut().ContarFalhasAsync("origem:x", default)).Should().Be(0);
    }

    [Fact]
    public async Task ContarFalhas_ItemDentroDaJanela_DeveRetornarFalhas()
    {
        ItemNaTabela(new()
        {
            ["chave"] = new AttributeValue { S = "origem:x" },
            ["falhas"] = new AttributeValue { N = "4" },
            ["expira_em"] = new AttributeValue { N = (_relogio.Agora.ToUnixTimeSeconds() + 60).ToString() }
        });

        (await Sut().ContarFalhasAsync("origem:x", default)).Should().Be(4);
    }

    [Fact]
    public async Task ContarFalhas_ItemExpiradoMasAindaNaoRemovidoPeloTtl_DeveSerZero()
    {
        // O TTL do DynamoDB apaga com atraso (até 48h). O código precisa checar expira_em.
        ItemNaTabela(new()
        {
            ["chave"] = new AttributeValue { S = "origem:x" },
            ["falhas"] = new AttributeValue { N = "9" },
            ["expira_em"] = new AttributeValue { N = (_relogio.Agora.ToUnixTimeSeconds() - 1).ToString() }
        });

        (await Sut().ContarFalhasAsync("origem:x", default)).Should().Be(0);
    }

    [Fact]
    public async Task ContarFalhas_DeveUsarLeituraConsistente()
    {
        ItemNaTabela(null);
        await Sut().ContarFalhasAsync("origem:x", default);

        _dynamo.Verify(d => d.GetItemAsync(
            It.Is<GetItemRequest>(r => r.TableName == Tabela && r.ConsistentRead == true && r.Key["chave"].S == "origem:x"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarFalha_DeveIncrementarAtomicamenteEFixarExpiracaoSoNaPrimeira()
    {
        ItemNaTabela(null); // sem item anterior: caminho do UpdateItem atômico
        UpdateItemRequest? capturado = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
               .Callback<UpdateItemRequest, CancellationToken>((r, _) => capturado = r)
               .ReturnsAsync(new UpdateItemResponse());

        await Sut().RegistrarFalhaAsync("identidade:admin", TimeSpan.FromMinutes(15), default);

        capturado.Should().NotBeNull();
        capturado!.TableName.Should().Be(Tabela);
        capturado.Key["chave"].S.Should().Be("identidade:admin");
        capturado.UpdateExpression.Should().Be("SET expira_em = if_not_exists(expira_em, :expira) ADD falhas :um");
        capturado.ExpressionAttributeValues[":um"].N.Should().Be("1");
        capturado.ExpressionAttributeValues[":expira"].N.Should().Be((_relogio.Agora.ToUnixTimeSeconds() + 900).ToString());
    }

    [Fact]
    public async Task RegistrarFalha_ComJanelaExpirada_DeveReiniciarOContador()
    {
        // Item velho (expira_em no passado) ainda não removido: a nova falha começa janela nova.
        ItemNaTabela(new()
        {
            ["chave"] = new AttributeValue { S = "origem:x" },
            ["falhas"] = new AttributeValue { N = "9" },
            ["expira_em"] = new AttributeValue { N = (_relogio.Agora.ToUnixTimeSeconds() - 1).ToString() }
        });
        PutItemRequest? put = null;
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
               .Callback<PutItemRequest, CancellationToken>((r, _) => put = r)
               .ReturnsAsync(new PutItemResponse());

        await Sut().RegistrarFalhaAsync("origem:x", TimeSpan.FromMinutes(15), default);

        put.Should().NotBeNull();
        put!.Item["falhas"].N.Should().Be("1");
        put.Item["expira_em"].N.Should().Be((_relogio.Agora.ToUnixTimeSeconds() + 900).ToString());
        _dynamo.Verify(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Limpar_DeveApagarOItem()
    {
        _dynamo.Setup(d => d.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new DeleteItemResponse());

        await Sut().LimparAsync("identidade:admin", default);

        _dynamo.Verify(d => d.DeleteItemAsync(
            It.Is<DeleteItemRequest>(r => r.TableName == Tabela && r.Key["chave"].S == "identidade:admin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
