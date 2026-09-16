using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Api.Testes;

public class AuthClienteEndpointTestes : IDisposable
{
    private readonly AuthApiFixture _app = new();
    private readonly HttpClient _http;

    public AuthClienteEndpointTestes() => _http = _app.CreateClient();
    public void Dispose() => _app.Dispose();

    private static object Corpo(string? cpf) => new { cpf };

    [Fact]
    public async Task ClienteAtivo_Deve200ComTokenEmSnakeCase()
    {
        var id = Guid.NewGuid();
        _app.Clientes.PorDocumento["39053344705"] = new ClienteAutenticavel(id, "Joao", "39053344705", true);

        var resposta = await _http.PostAsJsonAsync("/auth/cliente", Corpo("390.533.447-05"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("token_type").GetString().Should().Be("Bearer");
        json.GetProperty("expires_in").GetInt32().Should().Be(3600);
        json.GetProperty("perfil").GetString().Should().Be("Cliente");

        var jwt = new JsonWebToken(json.GetProperty("access_token").GetString()!);
        jwt.Subject.Should().Be(id.ToString());
        jwt.GetPayloadValue<string>("documento").Should().Be("39053344705");
        jwt.Issuer.Should().Be("oficina-auth");
    }

    [Theory]
    [InlineData("11111111111")]
    [InlineData("123")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CpfInvalido_Deve400ProblemDetails(string? cpf)
    {
        var resposta = await _http.PostAsJsonAsync("/auth/cliente", Corpo(cpf));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("status").GetInt32().Should().Be(400);
        problema.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CorpoNaoJson_Deve400()
    {
        var resposta = await _http.PostAsync("/auth/cliente", new StringContent("isto nao e json", System.Text.Encoding.UTF8, "application/json"));
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ClienteInexistente_Deve404()
    {
        var resposta = await _http.PostAsJsonAsync("/auth/cliente", Corpo("39053344705"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString().Should().Be("Cliente não encontrado.");
    }

    [Fact]
    public async Task ClienteInativo_Deve403()
    {
        _app.Clientes.PorDocumento["39053344705"] = new ClienteAutenticavel(Guid.NewGuid(), "Joao", "39053344705", Ativo: false);

        var resposta = await _http.PostAsJsonAsync("/auth/cliente", Corpo("39053344705"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AposMaximoDeFalhasDaOrigem_Deve429ComRetryAfter()
    {
        for (var i = 0; i < _app.MaximoPorOrigem; i++)
            (await _http.PostAsJsonAsync("/auth/cliente", Corpo("39053344705"))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var bloqueado = await _http.PostAsJsonAsync("/auth/cliente", Corpo("39053344705"));

        bloqueado.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        bloqueado.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task FalhaDeInfraestrutura_Deve500SemVazarDetalhe()
    {
        _app.Clientes.FalhaSimulada = new InvalidOperationException("Host=rds-interno;Password=xyz");

        var resposta = await _http.PostAsJsonAsync("/auth/cliente", Corpo("39053344705"));

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var texto = await resposta.Content.ReadAsStringAsync();
        texto.Should().NotContain("Password=xyz");
        texto.Should().NotContain("InvalidOperationException");
    }

    [Fact]
    public async Task MetodoGet_Deve405()
    {
        var resposta = await _http.GetAsync("/auth/cliente");
        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
