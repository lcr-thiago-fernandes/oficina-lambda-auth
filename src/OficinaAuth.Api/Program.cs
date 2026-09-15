using System.Text.Json;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using OficinaAuth.Api.Configuracao;
using OficinaAuth.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Logs JSON em uma linha (CloudWatch + New Relic), com escopo para o correlationId.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
});

// Sem efeito fora da Lambda (AWS_LAMBDA_FUNCTION_NAME ausente): a API sobe em Kestrel/TestServer.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeErros>();
builder.Services.AdicionarAutenticacao(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<MiddlewareDeCorrelacao>();
app.MapAuthEndpoints();

app.Run();

/// <summary>Exposta para WebApplicationFactory nos testes.</summary>
public partial class Program { }
