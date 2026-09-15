using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura.Tentativas;

/// <summary>
/// Item: { chave (S, PK), falhas (N), expira_em (N, epoch segundos — atributo TTL) }.
/// O TTL do DynamoDB remove itens expirados com atraso de até 48h, então TODA leitura
/// confere <c>expira_em</c> em código; o TTL é só faxina.
/// </summary>
public sealed class LimitadorDeTentativasDynamoDb : ILimitadorDeTentativas
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tabela;
    private readonly IRelogio _relogio;

    public LimitadorDeTentativasDynamoDb(IAmazonDynamoDB dynamo, string tabela, IRelogio relogio)
    {
        _dynamo = dynamo;
        _tabela = tabela;
        _relogio = relogio;
    }

    private static Dictionary<string, AttributeValue> Chave(string chave) =>
        new() { ["chave"] = new AttributeValue { S = chave } };

    private async Task<(int Falhas, long ExpiraEm)?> LerAsync(string chave, CancellationToken ct)
    {
        var resposta = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tabela,
            Key = Chave(chave),
            ConsistentRead = true
        }, ct);

        // SDK v4 pode devolver Item null (em vez de vazio) quando não há registro.
        if (resposta?.Item is null || resposta.Item.Count == 0)
            return null;

        var falhas = int.Parse(resposta.Item["falhas"].N);
        var expiraEm = long.Parse(resposta.Item["expira_em"].N);
        return (falhas, expiraEm);
    }

    public async Task<int> ContarFalhasAsync(string chave, CancellationToken ct)
    {
        var item = await LerAsync(chave, ct);
        if (item is null || item.Value.ExpiraEm <= _relogio.Agora.ToUnixTimeSeconds())
            return 0;
        return item.Value.Falhas;
    }

    public async Task RegistrarFalhaAsync(string chave, TimeSpan janela, CancellationToken ct)
    {
        var agora = _relogio.Agora.ToUnixTimeSeconds();
        var expira = (agora + (long)janela.TotalSeconds).ToString();

        var atual = await LerAsync(chave, ct);
        if (atual is not null && atual.Value.ExpiraEm <= agora)
        {
            // Janela anterior venceu mas o TTL ainda não apagou: recomeça do 1.
            await _dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = _tabela,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["chave"] = new AttributeValue { S = chave },
                    ["falhas"] = new AttributeValue { N = "1" },
                    ["expira_em"] = new AttributeValue { N = expira }
                }
            }, ct);
            return;
        }

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tabela,
            Key = Chave(chave),
            UpdateExpression = "SET expira_em = if_not_exists(expira_em, :expira) ADD falhas :um",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expira"] = new AttributeValue { N = expira },
                [":um"] = new AttributeValue { N = "1" }
            }
        }, ct);
    }

    public Task LimparAsync(string chave, CancellationToken ct) =>
        _dynamo.DeleteItemAsync(new DeleteItemRequest { TableName = _tabela, Key = Chave(chave) }, ct);
}
