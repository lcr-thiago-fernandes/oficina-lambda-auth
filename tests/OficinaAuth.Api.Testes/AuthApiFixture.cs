using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Dominio;

namespace OficinaAuth.Api.Testes;

public sealed class ClienteRepositorioFalso : IClienteRepositorio
{
    public ConcurrentDictionary<string, ClienteAutenticavel> PorDocumento { get; } = new();
    public Exception? FalhaSimulada { get; set; }

    public Task<ClienteAutenticavel?> ObterPorDocumentoAsync(string documento, CancellationToken ct)
    {
        if (FalhaSimulada is not null) throw FalhaSimulada;
        return Task.FromResult(PorDocumento.TryGetValue(documento, out var c) ? c : null);
    }
}

public sealed class UsuarioRepositorioFalso : IUsuarioRepositorio
{
    public ConcurrentDictionary<string, UsuarioAutenticavel> PorUsername { get; } = new();

    public Task<UsuarioAutenticavel?> ObterPorUsernameAsync(string username, CancellationToken ct) =>
        Task.FromResult(PorUsername.TryGetValue(username, out var u) ? u : null);
}

/// <summary>
/// TestServer não preenche <c>HttpContext.Connection.RemoteIpAddress</c> (fica nulo), o que
/// desde a correção da ProtecaoContraForcaBruta faz a checagem por origem ser pulada. Este
/// filtro injeta um IP fixo antes do pipeline da aplicação, para os testes que dependem do
/// limite por origem (ex.: <see cref="AuthClienteEndpointTestes.AposMaximoDeFalhasDaOrigem_Deve429ComRetryAfter"/>)
/// continuarem exercitando esse caminho.
/// </summary>
public sealed class OrigemFalsaStartupFilter : IStartupFilter
{
    public static readonly IPAddress Ip = IPAddress.Parse("203.0.113.10");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((ctx, proximo) =>
        {
            ctx.Connection.RemoteIpAddress = Ip;
            return proximo(ctx);
        });
        next(app);
    };
}

/// <summary>
/// Sobe a API em memória (TestServer). Segredos vêm de configuração (origem Ambiente),
/// o limitador é o de memória e os repositórios são fakes — o schema do banco já é
/// coberto pelos testes de Infraestrutura.
/// </summary>
public sealed class AuthApiFixture : WebApplicationFactory<Program>
{
    public const string Segredo = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";

    public ClienteRepositorioFalso Clientes { get; } = new();
    public UsuarioRepositorioFalso Usuarios { get; } = new();

    /// <summary>Limite por origem baixo para o teste de 429 ser curto.</summary>
    public int MaximoPorOrigem { get; init; } = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        builder.UseSetting("Segredos:Origem", "Ambiente");
        builder.UseSetting("JWT_SECRET", Segredo);
        builder.UseSetting("Tentativas:Armazenamento", "Memoria");
        builder.UseSetting("Tentativas:MaximoPorOrigem", MaximoPorOrigem.ToString());
        builder.UseSetting("Tentativas:MaximoPorIdentidade", "2");
        builder.UseSetting("Banco:ConnectionString", "Host=localhost;Database=nao-usado");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClienteRepositorio>();
            services.RemoveAll<IUsuarioRepositorio>();
            services.AddSingleton<IClienteRepositorio>(Clientes);
            services.AddSingleton<IUsuarioRepositorio>(Usuarios);
            services.AddSingleton<IStartupFilter>(new OrigemFalsaStartupFilter());
        });
    }
}
