using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace OficinaAuth.Authorizer;

/// <summary>Lê oficina/jwt_secret uma vez por processo (cache estático, como na auth-api).</summary>
public sealed class LeitorDeSegredo
{
    public const string NomeDoSegredo = "oficina/jwt_secret";

    private static Lazy<Task<string>>? _cache;
    private static readonly object Trava = new();

    private readonly IAmazonSecretsManager _cliente;

    public LeitorDeSegredo(IAmazonSecretsManager cliente) => _cliente = cliente;

    public async Task<string> ObterJwtAsync(CancellationToken ct)
    {
        Lazy<Task<string>> lazy;
        lock (Trava)
        {
            _cache ??= new Lazy<Task<string>>(LerAsync);
            lazy = _cache;
        }

        // Captura a tarefa ANTES do try: se só o ct do CHAMADOR cancelar o WaitAsync abaixo,
        // a tarefa de leitura em si continua rodando (e pode terminar com sucesso depois) —
        // nesse caso não podemos descartar o cache, senão a próxima chamada relê o segredo
        // à toa (ou, pior, descarta um resultado válido que estava prestes a chegar).
        var tarefa = lazy.Value;
        try
        {
            return await tarefa.WaitAsync(ct);
        }
        catch when (tarefa.IsFaulted || tarefa.IsCanceled)
        {
            // Aqui a tarefa de leitura em si falhou ou foi cancelada: o cache está inválido
            // para todo mundo, não só para este chamador. Evictar para a próxima chamada tentar de novo.
            lock (Trava) { if (ReferenceEquals(_cache, lazy)) _cache = null; }
            throw;
        }
    }

    private async Task<string> LerAsync()
    {
        var resposta = await _cliente.GetSecretValueAsync(new GetSecretValueRequest { SecretId = NomeDoSegredo });
        if (string.IsNullOrWhiteSpace(resposta.SecretString))
            throw new InvalidOperationException($"Segredo '{NomeDoSegredo}' veio vazio.");
        if (resposta.SecretString.TrimStart().StartsWith('{'))
            throw new InvalidOperationException($"Segredo '{NomeDoSegredo}' está em JSON; o contrato exige texto puro.");
        return resposta.SecretString;
    }

    public static void LimparCacheParaTestes()
    {
        lock (Trava) { _cache = null; }
    }
}
