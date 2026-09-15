using System.Collections.Concurrent;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura.Segredos;

/// <summary>
/// Lê do AWS Secrets Manager com cache ESTÁTICO por processo: o container da Lambda
/// reaproveita o valor entre invocações (mitigação de cold start do design, seção 4).
/// Rotacionar o segredo exige novo deploy ou reciclagem dos containers — aceito e
/// documentado no README.
/// </summary>
public sealed class ProvedorDeSegredosSecretsManager : IProvedorDeSegredos
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Cache = new();

    private readonly IAmazonSecretsManager _cliente;

    public ProvedorDeSegredosSecretsManager(IAmazonSecretsManager cliente) => _cliente = cliente;

    public async Task<string> ObterAsync(string nome, CancellationToken ct)
    {
        var lazy = Cache.GetOrAdd(nome, n => new Lazy<Task<string>>(() => LerAsync(n)));
        var tarefa = lazy.Value;
        try
        {
            return await tarefa.WaitAsync(ct);
        }
        catch when (tarefa.IsFaulted || tarefa.IsCanceled)
        {
            // Só descarta do cache se a tarefa COMPARTILHADA falhou (ou foi cancelada).
            // Se em vez disso foi só o ct do CHAMADOR que expirou enquanto a busca
            // compartilhada ainda rodava (e pode terminar com sucesso para outra
            // invocação), o filtro acima é falso e a exceção propaga sem descartar o
            // cache — evita disparar uma segunda busca concorrente no Secrets Manager.
            Cache.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(nome, lazy));
            throw;
        }
    }

    private async Task<string> LerAsync(string nome)
    {
        var resposta = await _cliente.GetSecretValueAsync(new GetSecretValueRequest { SecretId = nome });
        var valor = resposta.SecretString;

        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Segredo '{nome}' veio vazio do Secrets Manager.");

        var aparado = valor.TrimStart();
        if (aparado.StartsWith('{') || aparado.StartsWith('['))
            throw new InvalidOperationException(
                $"Segredo '{nome}' está gravado como JSON. O contrato entre repositórios exige texto puro " +
                "(--query SecretString --output text no CD do oficina-app lê o valor inteiro).");

        return valor;
    }

    /// <summary>Só para testes isolarem o cache estático entre casos.</summary>
    public static void LimparCacheParaTestes() => Cache.Clear();
}
