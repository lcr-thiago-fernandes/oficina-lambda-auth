namespace OficinaAuth.Dominio;

/// <summary>
/// Valores da claim <c>perfil</c>. Case-sensitive: a política RequerCliente do
/// oficina-app compara com <c>RequireClaim("perfil", "Cliente")</c>.
/// </summary>
public static class Perfis
{
    public const string Cliente = "Cliente";
    public const string Atendente = "Atendente";
    public const string Admin = "Admin";

    /// <summary>Perfis que existem em <c>auth.usuario.perfil</c> (enum Perfil do oficina-app).</summary>
    public static bool EhAdministrativo(string perfil) => perfil is Admin or Atendente;
}
