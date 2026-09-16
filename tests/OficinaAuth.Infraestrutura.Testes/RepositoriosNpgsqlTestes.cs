using FluentAssertions;
using OficinaAuth.Infraestrutura.Persistencia;

namespace OficinaAuth.Infraestrutura.Testes;

public class RepositoriosNpgsqlTestes : IClassFixture<BancoFixture>
{
    private readonly BancoFixture _banco;
    private readonly FabricaDeConexao _fabrica;

    public RepositoriosNpgsqlTestes(BancoFixture banco)
    {
        _banco = banco;
        _fabrica = new FabricaDeConexao(banco.ConnectionString);
    }

    [Fact]
    public async Task Cliente_ObterPorDocumento_DeveProjetarIdNomeDocumentoAtivo()
    {
        var id = Guid.NewGuid();
        await _banco.InserirClienteAsync(id, "Maria", "11144477735", ativo: true);

        var cliente = await new ClienteRepositorioNpgsql(_fabrica).ObterPorDocumentoAsync("11144477735", default);

        cliente.Should().NotBeNull();
        cliente!.Id.Should().Be(id);
        cliente.Nome.Should().Be("Maria");
        cliente.Documento.Should().Be("11144477735");
        cliente.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Cliente_Inativo_DeveVirComAtivoFalse()
    {
        await _banco.InserirClienteAsync(Guid.NewGuid(), "Inativo", "12345678909", ativo: false);

        var cliente = await new ClienteRepositorioNpgsql(_fabrica).ObterPorDocumentoAsync("12345678909", default);

        cliente!.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task Cliente_Inexistente_DeveRetornarNull()
    {
        var cliente = await new ClienteRepositorioNpgsql(_fabrica).ObterPorDocumentoAsync("52998224725", default);
        cliente.Should().BeNull();
    }

    [Fact]
    public async Task Usuario_ObterPorUsername_DeveProjetarHashPerfilEAtivo()
    {
        var id = Guid.NewGuid();
        await _banco.InserirUsuarioAsync(id, "atendente1", "$2a$12$hashqualquer", "Atendente", ativo: true);

        var usuario = await new UsuarioRepositorioNpgsql(_fabrica).ObterPorUsernameAsync("atendente1", default);

        usuario.Should().NotBeNull();
        usuario!.Id.Should().Be(id);
        usuario.Username.Should().Be("atendente1");
        usuario.PasswordHash.Should().Be("$2a$12$hashqualquer");
        usuario.Perfil.Should().Be("Atendente");
        usuario.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_Inexistente_DeveRetornarNull()
    {
        var usuario = await new UsuarioRepositorioNpgsql(_fabrica).ObterPorUsernameAsync("ninguem", default);
        usuario.Should().BeNull();
    }

    [Fact]
    public async Task Usuario_BuscaEhCaseSensitiveComoOIndiceUnicoDaApi()
    {
        // A API grava sempre minúsculo (Username.Criar). Buscar "ADMIN" não pode achar "admin":
        // quem normaliza é o caso de uso, não o SQL.
        await _banco.InserirUsuarioAsync(Guid.NewGuid(), "admin", "$2a$12$x", "Admin", true);

        var usuario = await new UsuarioRepositorioNpgsql(_fabrica).ObterPorUsernameAsync("ADMIN", default);

        usuario.Should().BeNull();
    }

    [Fact]
    public async Task FabricaDeConexao_DeveReutilizarOMesmoDataSource()
    {
        var a = await _fabrica.ObterAsync(default);
        var b = await _fabrica.ObterAsync(default);
        a.Should().BeSameAs(b);
    }
}
