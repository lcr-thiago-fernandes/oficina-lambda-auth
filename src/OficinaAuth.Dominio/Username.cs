using System.Text.RegularExpressions;

namespace OficinaAuth.Dominio;

public sealed class Username : IEquatable<Username>
{
    private static readonly Regex Padrao = new("^[a-z0-9._]+$", RegexOptions.Compiled);

    public string Valor { get; }

    private Username(string valor)
    {
        Valor = valor;
    }

    public static Username Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("Username não pode ser vazio.", nameof(valor));

        var normalizado = valor.Trim().ToLowerInvariant();

        if (normalizado.Length < 3 || normalizado.Length > 50)
            throw new ArgumentException(
                "Username deve ter entre 3 e 50 caracteres.", nameof(valor));

        if (!Padrao.IsMatch(normalizado))
            throw new ArgumentException(
                "Username só aceita letras, dígitos, ponto e underscore.", nameof(valor));

        return new Username(normalizado);
    }

    public bool Equals(Username? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => Equals(obj as Username);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;
}
