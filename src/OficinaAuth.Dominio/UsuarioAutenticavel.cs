namespace OficinaAuth.Dominio;

/// <summary>Projeção de <c>auth.usuario</c> com o mínimo necessário para autenticar.</summary>
public sealed record UsuarioAutenticavel(Guid Id, string Username, string PasswordHash, string Perfil, bool Ativo);
