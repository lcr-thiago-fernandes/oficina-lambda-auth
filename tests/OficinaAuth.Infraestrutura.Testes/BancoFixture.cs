using Npgsql;
using Testcontainers.PostgreSql;

namespace OficinaAuth.Infraestrutura.Testes;

/// <summary>
/// Sobe um PostgreSQL 16 e cria SOMENTE as duas tabelas que esta Lambda lê, com o DDL
/// idêntico ao gerado pelas migrations InicialAuth e InicialClientes do oficina-app.
/// Este DDL é o contrato: se a API renomear coluna/tabela, o teste aqui continua
/// verde — por isso o CONTRATO está escrito em docs/contratos.md e a mudança lá exige
/// mudança aqui.
/// </summary>
public sealed class BancoFixture : IAsyncLifetime
{
    // Testcontainers 4.15: o construtor sem parametros ficou obsoleto (CS0618, erro com TreatWarningsAsErrors);
    // a imagem vai no construtor.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("oficina")
        .WithUsername("oficina_admin")
        .WithPassword("senha-de-teste")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public const string Ddl = """
        CREATE SCHEMA IF NOT EXISTS auth;
        CREATE TABLE auth.usuario (
            id uuid NOT NULL,
            username character varying(50) NOT NULL,
            password_hash character varying(255) NOT NULL,
            perfil character varying(20) NOT NULL,
            ativo boolean NOT NULL,
            precisa_trocar_senha boolean NOT NULL,
            criado_em timestamp with time zone NOT NULL,
            CONSTRAINT "PK_usuario" PRIMARY KEY (id)
        );
        CREATE UNIQUE INDEX "IX_usuario_username" ON auth.usuario (username);

        CREATE SCHEMA IF NOT EXISTS clientes;
        CREATE TABLE clientes.cliente (
            id uuid NOT NULL,
            nome character varying(150) NOT NULL,
            documento character varying(14) NOT NULL,
            tipo_pessoa character varying(2) NOT NULL,
            email character varying(150) NOT NULL,
            telefone character varying(20) NOT NULL,
            ativo boolean NOT NULL,
            criado_em timestamp with time zone NOT NULL,
            atualizado_em timestamp with time zone NOT NULL,
            CONSTRAINT "PK_cliente" PRIMARY KEY (id)
        );
        CREATE UNIQUE INDEX "IX_cliente_documento" ON clientes.cliente (documento);
        """;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(Ddl, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task InserirClienteAsync(Guid id, string nome, string documento, bool ativo)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO clientes.cliente (id, nome, documento, tipo_pessoa, email, telefone, ativo, criado_em, atualizado_em)
            VALUES (@id, @nome, @documento, 'PF', 'x@x.com', '11999999999', @ativo, now(), now())
            """, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("nome", nome);
        cmd.Parameters.AddWithValue("documento", documento);
        cmd.Parameters.AddWithValue("ativo", ativo);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InserirUsuarioAsync(Guid id, string username, string passwordHash, string perfil, bool ativo)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO auth.usuario (id, username, password_hash, perfil, ativo, precisa_trocar_senha, criado_em)
            VALUES (@id, @username, @hash, @perfil, @ativo, true, now())
            """, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        cmd.Parameters.AddWithValue("perfil", perfil);
        cmd.Parameters.AddWithValue("ativo", ativo);
        await cmd.ExecuteNonQueryAsync();
    }
}
