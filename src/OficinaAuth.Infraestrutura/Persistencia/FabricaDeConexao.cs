using Npgsql;
using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura.Persistencia;

/// <summary>Host, porta, database e usuário do RDS. A senha vem do Secrets Manager.</summary>
public sealed record ParametrosDoBanco(string Host, int Porta, string Nome, string Usuario);

/// <summary>
/// Monta e cacheia um único <see cref="NpgsqlDataSource"/> por processo. Em Lambda o
/// container atende uma requisição por vez, então <c>Maximum Pool Size=2</c> basta e
/// evita abrir conexões que nunca serão usadas (cold start e limite do db.t3.micro).
/// </summary>
public sealed class FabricaDeConexao
{
    private readonly Lazy<Task<NpgsqlDataSource>> _dataSource;

    /// <summary>Uso em Lambda: senha resolvida sob demanda pelo provedor de segredos.</summary>
    public FabricaDeConexao(ParametrosDoBanco parametros, IProvedorDeSegredos segredos)
    {
        _dataSource = new Lazy<Task<NpgsqlDataSource>>(async () =>
        {
            var senha = await segredos.ObterAsync(NomesDeSegredos.SenhaDoBanco, CancellationToken.None);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = parametros.Host,
                Port = parametros.Porta,
                Database = parametros.Nome,
                Username = parametros.Usuario,
                Password = senha,
                MaxPoolSize = 2,
                Timeout = 5,
                CommandTimeout = 5,
                SslMode = SslMode.Prefer
            };
            return NpgsqlDataSource.Create(builder.ConnectionString);
        });
    }

    /// <summary>Uso local e em testes: connection string completa.</summary>
    public FabricaDeConexao(string connectionString)
    {
        _dataSource = new Lazy<Task<NpgsqlDataSource>>(() => Task.FromResult(NpgsqlDataSource.Create(connectionString)));
    }

    public async ValueTask<NpgsqlDataSource> ObterAsync(CancellationToken ct) => await _dataSource.Value.WaitAsync(ct);
}
