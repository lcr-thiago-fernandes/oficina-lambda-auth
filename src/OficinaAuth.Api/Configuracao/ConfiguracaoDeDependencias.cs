using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using OficinaAuth.Aplicacao;
using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tentativas;
using OficinaAuth.Aplicacao.Tokens;
using OficinaAuth.Infraestrutura;
using OficinaAuth.Infraestrutura.Persistencia;
using OficinaAuth.Infraestrutura.Segredos;
using OficinaAuth.Infraestrutura.Tentativas;

namespace OficinaAuth.Api.Configuracao;

public static class ConfiguracaoDeDependencias
{
    /// <summary>
    /// Tudo singleton de propósito: a Lambda atende uma requisição por container e os
    /// objetos são imutáveis ou thread-safe. Singleton também é o que faz o cache do
    /// segredo e o NpgsqlDataSource sobreviverem entre invocações.
    /// </summary>
    public static IServiceCollection AdicionarAutenticacao(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IRelogio, RelogioDoSistema>();

        // --- Segredos ---
        var origem = config["Segredos:Origem"] ?? "SecretsManager";
        switch (origem)
        {
            case "Ambiente":
                services.AddSingleton<IProvedorDeSegredos>(new ProvedorDeSegredosAmbiente(new Dictionary<string, string?>
                {
                    ["JWT_SECRET"] = config["JWT_SECRET"],
                    ["DB_PASSWORD"] = config["DB_PASSWORD"]
                }));
                break;
            case "SecretsManager":
                services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
                services.AddSingleton<IProvedorDeSegredos, ProvedorDeSegredosSecretsManager>();
                break;
            default:
                throw new InvalidOperationException($"Segredos:Origem='{origem}' inválido. Use Ambiente ou SecretsManager.");
        }

        // --- Banco ---
        var connectionString = config["Banco:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton(new FabricaDeConexao(connectionString));
        }
        else
        {
            var parametros = new ParametrosDoBanco(
                Host: config["Banco:Host"] ?? throw new InvalidOperationException("Banco:Host não configurado."),
                Porta: config.GetValue("Banco:Porta", 5432),
                Nome: config["Banco:Nome"] ?? "oficina",
                Usuario: config["Banco:Usuario"] ?? "oficina_admin");
            services.AddSingleton(sp => new FabricaDeConexao(parametros, sp.GetRequiredService<IProvedorDeSegredos>()));
        }
        services.AddSingleton<IClienteRepositorio, ClienteRepositorioNpgsql>();
        services.AddSingleton<IUsuarioRepositorio, UsuarioRepositorioNpgsql>();

        // --- Tentativas ---
        var politica = new PoliticaDeTentativas(
            MaximoPorOrigem: config.GetValue("Tentativas:MaximoPorOrigem", PoliticaDeTentativas.Padrao.MaximoPorOrigem),
            MaximoPorIdentidade: config.GetValue("Tentativas:MaximoPorIdentidade", PoliticaDeTentativas.Padrao.MaximoPorIdentidade),
            Janela: TimeSpan.FromMinutes(config.GetValue("Tentativas:JanelaMinutos", (int)PoliticaDeTentativas.Padrao.Janela.TotalMinutes)));
        services.AddSingleton(politica);

        var armazenamento = config["Tentativas:Armazenamento"] ?? "DynamoDb";
        switch (armazenamento)
        {
            case "Memoria":
                services.AddSingleton<ILimitadorDeTentativas, LimitadorDeTentativasMemoria>();
                break;
            case "DynamoDb":
                var tabela = config["Tentativas:Tabela"]
                    ?? throw new InvalidOperationException("Tentativas:Tabela não configurada para o armazenamento DynamoDb.");
                services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
                services.AddSingleton<ILimitadorDeTentativas>(sp =>
                    new LimitadorDeTentativasDynamoDb(sp.GetRequiredService<IAmazonDynamoDB>(), tabela, sp.GetRequiredService<IRelogio>()));
                break;
            default:
                throw new InvalidOperationException($"Tentativas:Armazenamento='{armazenamento}' inválido. Use Memoria ou DynamoDb.");
        }
        services.AddSingleton<ProtecaoContraForcaBruta>();

        // --- Casos de uso ---
        services.AddSingleton<EmissorDeToken>();
        services.AddSingleton<AutenticarClienteUseCase>();
        services.AddSingleton<AutenticarAdminUseCase>();

        return services;
    }
}
