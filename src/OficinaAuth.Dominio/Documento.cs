using System.Text.RegularExpressions;

namespace OficinaAuth.Dominio;

public sealed class Documento : IEquatable<Documento>
{
    public string Valor { get; }
    public TipoPessoa Tipo { get; }

    private Documento(string valor, TipoPessoa tipo)
    {
        Valor = valor;
        Tipo = tipo;
    }

    public static Documento Criar(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new DocumentoInvalidoException("Documento não pode ser vazio.");

        var digitos = Regex.Replace(entrada, @"\D", "");

        return digitos.Length switch
        {
            11 when ValidaCpf(digitos) => new Documento(digitos, TipoPessoa.PF),
            14 when ValidaCnpj(digitos) => new Documento(digitos, TipoPessoa.PJ),
            11 or 14 => throw new DocumentoInvalidoException(
                $"Documento '{entrada}' falhou na validação dos dígitos verificadores."),
            _ => throw new DocumentoInvalidoException(
                $"Documento '{entrada}' tem tamanho inválido (esperado 11 ou 14 dígitos).")
        };
    }

    public string Mascarado() => Tipo switch
    {
        TipoPessoa.PF => Convert.ToUInt64(Valor).ToString(@"000\.000\.000\-00"),
        TipoPessoa.PJ => Convert.ToUInt64(Valor).ToString(@"00\.000\.000\/0000\-00"),
        _ => Valor
    };

    private static bool ValidaCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1) return false;

        int[] m1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] m2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var temp = cpf[..9];
        var soma = m1.Select((m, i) => m * (temp[i] - '0')).Sum();
        var resto = soma % 11;
        var d1 = resto < 2 ? 0 : 11 - resto;
        temp += d1;

        soma = m2.Select((m, i) => m * (temp[i] - '0')).Sum();
        resto = soma % 11;
        var d2 = resto < 2 ? 0 : 11 - resto;

        return cpf.EndsWith($"{d1}{d2}");
    }

    private static bool ValidaCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1) return false;

        int[] m1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] m2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var temp = cnpj[..12];
        var soma = m1.Select((m, i) => m * (temp[i] - '0')).Sum();
        var resto = soma % 11;
        var d1 = resto < 2 ? 0 : 11 - resto;
        temp += d1;

        soma = m2.Select((m, i) => m * (temp[i] - '0')).Sum();
        resto = soma % 11;
        var d2 = resto < 2 ? 0 : 11 - resto;

        return cnpj.EndsWith($"{d1}{d2}");
    }

    public bool Equals(Documento? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => Equals(obj as Documento);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;
}
