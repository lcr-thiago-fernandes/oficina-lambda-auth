using System.Net.Http.Json;
using FluentAssertions;

namespace OficinaAuth.Api.Testes;

public class CorrelacaoEErrosTestes : IDisposable
{
    private readonly AuthApiFixture _app = new();
    private readonly HttpClient _http;

    public CorrelacaoEErrosTestes() => _http = _app.CreateClient();
    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task HeaderDeCorrelacaoEnviado_DeveSerEcoado()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/auth/cliente") { Content = JsonContent.Create(new { cpf = "39053344705" }) };
        req.Headers.Add("X-Correlation-Id", "abc-123");

        var resposta = await _http.SendAsync(req);

        resposta.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be("abc-123");
    }

    [Fact]
    public async Task SemHeaderDeCorrelacao_DeveGerarUm()
    {
        var resposta = await _http.PostAsJsonAsync("/auth/cliente", new { cpf = "39053344705" });

        resposta.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RotaInexistente_Deve404()
    {
        var resposta = await _http.GetAsync("/nao-existe");
        resposta.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
