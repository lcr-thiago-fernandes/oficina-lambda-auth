using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Dominio;

namespace OficinaAuth.Infraestrutura.Persistencia;

public sealed class ClienteRepositorioNpgsql : IClienteRepositorio
{
    // CONTRATO com oficina-app (ClienteConfiguration.cs): tabela clientes.cliente,
    // colunas id, nome, documento (só dígitos, índice único), ativo.
    private const string Sql = """
        SELECT id, nome, documento, ativo
          FROM clientes.cliente
         WHERE documento = @documento
        """;

    private readonly FabricaDeConexao _fabrica;

    public ClienteRepositorioNpgsql(FabricaDeConexao fabrica) => _fabrica = fabrica;

    public async Task<ClienteAutenticavel?> ObterPorDocumentoAsync(string documento, CancellationToken ct)
    {
        var dataSource = await _fabrica.ObterAsync(ct);
        await using var cmd = dataSource.CreateCommand(Sql);
        cmd.Parameters.AddWithValue("documento", documento);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ClienteAutenticavel(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3));
    }
}
