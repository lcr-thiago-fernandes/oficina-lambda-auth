using FluentAssertions;
using OficinaAuth.Dominio;

namespace OficinaAuth.Dominio.Testes;

public class VerificadorDeSenhaTestes
{
    // Custo 12 = o mesmo que Senha.DeTextoPuro usa no oficina-app (BCryptCost = 12).
    private static readonly string HashDoBootstrap = BCrypt.Net.BCrypt.HashPassword("AlteraMe@123", 12);

    [Fact]
    public void Confere_ComSenhaCorreta_DeveRetornarTrue()
    {
        VerificadorDeSenha.Confere(HashDoBootstrap, "AlteraMe@123").Should().BeTrue();
    }

    [Fact]
    public void Confere_ComSenhaErrada_DeveRetornarFalse()
    {
        VerificadorDeSenha.Confere(HashDoBootstrap, "outra-senha").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Confere_ComSenhaVazia_DeveRetornarFalseSemLancar(string? senha)
    {
        VerificadorDeSenha.Confere(HashDoBootstrap, senha).Should().BeFalse();
    }

    [Fact]
    public void Confere_ComHashMalformado_DeveRetornarFalseSemLancar()
    {
        VerificadorDeSenha.Confere("nao-e-um-hash-bcrypt", "qualquer").Should().BeFalse();
    }

    [Fact]
    public void HashFicticio_DeveSerUmHashBCryptValidoQueNaoConfereComNada()
    {
        VerificadorDeSenha.HashFicticio.Should().StartWith("$2");
        VerificadorDeSenha.Confere(VerificadorDeSenha.HashFicticio, "ficticio").Should().BeFalse();
    }
}
