using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Api.Testes;

public class AuthAdminEndpointTestes : IDisposable
{
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword("AlteraMe@123", 4);

    private readonly AuthApiFixture _app = new();
    private readonly HttpClient _http;

    public AuthAdminEndpointTestes()
    {
        _http = _app.CreateClient();
        _app.Usuarios.PorUsername["admin"] = new UsuarioAutenticavel(Guid.NewGuid(), "admin", Hash, "Admin", true);
        _app.Usuarios.PorUsername["inativo"] = new UsuarioAutenticavel(Guid.NewGuid(), "inativo", Hash, "Atendente", false);
    }

    public void Dispose() => _app.Dispose();

    private static object Corpo(string? username, string? password) => new { username, password };

    [Fact]
    public async Task CredenciaisCorretas_Deve200ComPerfilDoBanco()
    {
        var resposta = await _http.PostAsJsonAsync("/auth/admin", Corpo("Admin", "AlteraMe@123"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("perfil").GetString().Should().Be("Admin");
        var jwt = new JsonWebToken(json.GetProperty("access_token").GetString()!);
        jwt.GetPayloadValue<string>("perfil").Should().Be("Admin");
        jwt.TryGetPayloadValue<string>("documento", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SenhaErrada_Deve401ComWwwAuthenticate()
    {
        var resposta = await _http.PostAsJsonAsync("/auth/admin", Corpo("admin", "errada"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Headers.WwwAuthenticate.ToString().Should().Contain("Bearer");
    }

    [Fact]
    public async Task UsuarioInexistente_DeveMesmo401()
    {
        var resposta = await _http.PostAsJsonAsync("/auth/admin", Corpo("fantasma", "x"));
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UsuarioInativo_Deve403()
    {
        var resposta = await _http.PostAsJsonAsync("/auth/admin", Corpo("inativo", "AlteraMe@123"));
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("admin", null)]
    [InlineData("", "")]
    public async Task CamposAusentes_Deve400(string? username, string? password)
    {
        var resposta = await _http.PostAsJsonAsync("/auth/admin", Corpo(username, password));
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AposMaximoDeFalhasDaIdentidade_Deve429()
    {
        (await _http.PostAsJsonAsync("/auth/admin", Corpo("admin", "errada1"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _http.PostAsJsonAsync("/auth/admin", Corpo("admin", "errada2"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var bloqueado = await _http.PostAsJsonAsync("/auth/admin", Corpo("admin", "AlteraMe@123"));

        bloqueado.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
