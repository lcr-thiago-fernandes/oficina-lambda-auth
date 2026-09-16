using Npgsql;
using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura.Persistencia;

/// <summary>Host, porta, database e usuário do RDS. A senha vem do Secrets Manager.</summary>
public sealed record ParametrosDoBanco(string Host, int Porta, string Nome, string Usuario);

/// <summary>
/// Monta e cacheia um único <see cref="NpgsqlDataSource"/> por processo. Em Lambda o
/// container atende uma requisição por vez, então <c>Maximum Pool Size=2</c> basta e
/// evita abrir conexões que nunca serão usadas (cold start e limite do db.t3.micro).
/// Uma falha na construção (ex.: Secrets Manager indisponível) NÃO fica em cache: a
/// próxima chamada tenta construir de novo, em vez de repetir a mesma exceção para
/// sempre no container morno do Lambda.
/// </summary>
public sealed class FabricaDeConexao
{
    private readonly Func<Task<NpgsqlDataSource>> _construir;
    private readonly SemaphoreSlim _portao = new(1, 1);
    private Task<NpgsqlDataSource>? _dataSource;

    /// <summary>Uso em Lambda: senha resolvida sob demanda pelo provedor de segredos.</summary>
    public FabricaDeConexao(ParametrosDoBanco parametros, IProvedorDeSegredos segredos)
    {
        _construir = async () =>
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
        };
    }

    /// <summary>Uso local e em testes: connection string completa.</summary>
    public FabricaDeConexao(string connectionString)
    {
        _construir = () => Task.FromResult(NpgsqlDataSource.Create(connectionString));
    }

    public async ValueTask<NpgsqlDataSource> ObterAsync(CancellationToken ct)
    {
        // O portão só protege a leitura/criação do campo `_dataSource` — uma seção
        // crítica trivial. A espera pela construção em si acontece FORA do portão,
        // para que o ct de um chamador (cancelamento, timeout) nunca bloqueie outro
        // chamador que está esperando a mesma tarefa compartilhada terminar.
        Task<NpgsqlDataSource> tarefa;
        await _portao.WaitAsync(ct);
        try
        {
            tarefa = _dataSource ??= _construir();
        }
        finally
        {
            _portao.Release();
        }

        try
        {
            return await tarefa.WaitAsync(ct);
        }
        catch when (tarefa.IsFaulted || tarefa.IsCanceled)
        {
            // A construção compartilhada em si falhou (ou foi cancelada): não cacheia,
            // a próxima chamada reconstrói do zero. Se em vez disso foi só o ct do
            // CHAMADOR que expirou enquanto a construção compartilhada ainda rodava (e
            // pode terminar com sucesso para outra chamada), o filtro acima é falso e a
            // exceção propaga sem descartar `_dataSource`.
            await DescartarAsync(tarefa);
            throw;
        }
    }

    /// <summary>Remove `tarefa` do cache, mas só se ninguém mais a substituiu antes.</summary>
    private async Task DescartarAsync(Task<NpgsqlDataSource> tarefa)
    {
        await _portao.WaitAsync(CancellationToken.None);
        try
        {
            if (ReferenceEquals(_dataSource, tarefa))
                _dataSource = null;
        }
        finally
        {
            _portao.Release();
        }
    }
}
