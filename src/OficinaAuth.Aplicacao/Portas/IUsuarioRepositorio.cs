using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao.Portas;

public interface IUsuarioRepositorio
{
    /// <param name="username">Já normalizado por <see cref="Username.Criar"/> (minúsculo, sem espaços).</param>
    Task<UsuarioAutenticavel?> ObterPorUsernameAsync(string username, CancellationToken ct);
}
