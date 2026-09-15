namespace OficinaAuth.Dominio;

/// <summary>
/// Confere uma senha em texto puro contra o hash BCrypt gravado em
/// <c>auth.usuario.password_hash</c> pelo bootstrap do oficina-app
/// (<c>Senha.DeTextoPuro</c>, custo 12). Só verifica; nunca gera hash de usuário.
/// </summary>
public static class VerificadorDeSenha
{
    /// <summary>
    /// Hash de uma senha aleatória, gerado uma vez por processo. Usado quando o usuário
    /// NÃO existe, para que o tempo de resposta seja o mesmo de uma senha errada e não
    /// revele quais usernames estão cadastrados.
    /// </summary>
    public static readonly string HashFicticio =
        BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), 12);

    public static bool Confere(string hashBCrypt, string? textoPuro)
    {
        if (string.IsNullOrEmpty(textoPuro) || string.IsNullOrWhiteSpace(hashBCrypt))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(textoPuro, hashBCrypt);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
