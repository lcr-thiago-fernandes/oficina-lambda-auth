using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Dominio;

namespace OficinaAuth.Infraestrutura.Persistencia;

public sealed class UsuarioRepositorioNpgsql : IUsuarioRepositorio
{
    // CONTRATO com oficina-app (UsuarioConfiguration.cs): tabela auth.usuario,
    // colunas id, username (minúsculo, índice único), password_hash (BCrypt), perfil
    // ("Admin" | "Atendente", string do enum), ativo.
    private const string Sql = """
        SELECT id, username, password_hash, perfil, ativo
          FROM auth.usuario
         WHERE username = @username
        """;

    private readonly FabricaDeConexao _fabrica;

    public UsuarioRepositorioNpgsql(FabricaDeConexao fabrica) => _fabrica = fabrica;

    public async Task<UsuarioAutenticavel?> ObterPorUsernameAsync(string username, CancellationToken ct)
    {
        var dataSource = await _fabrica.ObterAsync(ct);
        await using var cmd = dataSource.CreateCommand(Sql);
        cmd.Parameters.AddWithValue("username", username);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new UsuarioAutenticavel(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4));
    }
}
