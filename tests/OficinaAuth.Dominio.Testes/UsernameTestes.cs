using FluentAssertions;
using OficinaAuth.Dominio;

namespace OficinaAuth.Dominio.Testes;

public class UsernameTestes
{
    [Theory]
    [InlineData("Admin", "admin")]
    [InlineData("  joao.silva ", "joao.silva")]
    [InlineData("user_01", "user_01")]
    public void Criar_DeveNormalizarParaMinusculoSemEspacos(string entrada, string esperado)
    {
        Username.Criar(entrada).Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData("com espaco")]
    [InlineData("acento-ç")]
    public void Criar_ComValorInvalido_DeveLancarArgumentException(string entrada)
    {
        var act = () => Username.Criar(entrada);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ComMaisDe50Caracteres_DeveLancar()
    {
        var act = () => Username.Criar(new string('a', 51));
        act.Should().Throw<ArgumentException>();
    }
}
