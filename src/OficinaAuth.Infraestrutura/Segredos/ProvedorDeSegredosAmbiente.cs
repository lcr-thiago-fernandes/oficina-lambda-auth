using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura.Segredos;

/// <summary>Execução local: os segredos vêm de variáveis de ambiente (.env). Nunca usado em Lambda.</summary>
public sealed class ProvedorDeSegredosAmbiente : IProvedorDeSegredos
{
    private static readonly IReadOnlyDictionary<string, string> Mapa = new Dictionary<string, string>
    {
        [NomesDeSegredos.Jwt] = "JWT_SECRET",
        [NomesDeSegredos.SenhaDoBanco] = "DB_PASSWORD"
    };

    private readonly IReadOnlyDictionary<string, string?> _variaveis;

    public ProvedorDeSegredosAmbiente(IReadOnlyDictionary<string, string?> variaveis) => _variaveis = variaveis;

    public static ProvedorDeSegredosAmbiente DoProcesso()
    {
        var env = Environment.GetEnvironmentVariables();
        var dict = new Dictionary<string, string?>();
        foreach (var chave in env.Keys)
            dict[chave.ToString()!] = env[chave]?.ToString();
        return new ProvedorDeSegredosAmbiente(dict);
    }

    public Task<string> ObterAsync(string nome, CancellationToken ct)
    {
        if (!Mapa.TryGetValue(nome, out var variavel))
            throw new ArgumentException($"Segredo '{nome}' não tem variável de ambiente correspondente.", nameof(nome));

        if (!_variaveis.TryGetValue(variavel, out var valor) || string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Variável de ambiente {variavel} não definida (segredo '{nome}').");

        return Task.FromResult(valor);
    }
}
