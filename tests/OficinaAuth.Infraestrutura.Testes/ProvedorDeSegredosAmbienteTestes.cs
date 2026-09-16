using FluentAssertions;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Infraestrutura.Segredos;

namespace OficinaAuth.Infraestrutura.Testes;

public class ProvedorDeSegredosAmbienteTestes
{
    [Fact]
    public async Task Obter_DeveMapearNomeDoSecretsManagerParaVariavelDeAmbiente()
    {
        var sut = new ProvedorDeSegredosAmbiente(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "segredo-local",
            ["DB_PASSWORD"] = "senha-local"
        });

        (await sut.ObterAsync(NomesDeSegredos.Jwt, default)).Should().Be("segredo-local");
        (await sut.ObterAsync(NomesDeSegredos.SenhaDoBanco, default)).Should().Be("senha-local");
    }

    [Fact]
    public async Task Obter_SemAVariavel_DeveFalharComMensagemQueNomeiaAVariavel()
    {
        var sut = new ProvedorDeSegredosAmbiente(new Dictionary<string, string?>());

        var act = () => sut.ObterAsync(NomesDeSegredos.Jwt, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*JWT_SECRET*");
    }

    [Fact]
    public async Task Obter_NomeDesconhecido_DeveFalhar()
    {
        var sut = new ProvedorDeSegredosAmbiente(new Dictionary<string, string?>());
        var act = () => sut.ObterAsync("oficina/outro", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
