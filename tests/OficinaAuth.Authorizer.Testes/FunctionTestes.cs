using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OficinaAuth.Authorizer;

namespace OficinaAuth.Authorizer.Testes;

public class FunctionTestes
{
    private const string Segredo = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";
    private readonly Mock<IAmazonSecretsManager> _sm = new();
    private readonly ILambdaContext _contexto = Mock.Of<ILambdaContext>(c => c.AwsRequestId == "req-1");

    public FunctionTestes()
    {
        LeitorDeSegredo.LimparCacheParaTestes();
        _sm.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "oficina/jwt_secret"), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new GetSecretValueResponse { SecretString = Segredo });
    }

    private Function Sut() => new(new LeitorDeSegredo(_sm.Object));

    private static string TokenValido()
    {
        var agora = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "oficina-auth",
            Audience = "oficina-api",
            Claims = new Dictionary<string, object> { ["sub"] = "abc", ["perfil"] = "Atendente", ["nome"] = "x", ["jti"] = "1" },
            Expires = agora.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Segredo)), SecurityAlgorithms.HmacSha256)
        });
    }

    private static APIGatewayCustomAuthorizerV2Request Requisicao(string? authorization, string nomeDoHeader = "authorization") => new()
    {
        Type = "REQUEST",
        RouteArn = "arn:aws:execute-api:us-east-1:123:api/$default/GET/api/v1/me/veiculos",
        Headers = authorization is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [nomeDoHeader] = authorization }
    };

    [Fact]
    public async Task TokenValido_DeveAutorizarEDevolverContexto()
    {
        var resposta = await Sut().HandlerAsync(Requisicao($"Bearer {TokenValido()}"), _contexto);

        resposta.IsAuthorized.Should().BeTrue();
        resposta.Context["perfil"].Should().Be("Atendente");
        resposta.Context["sub"].Should().Be("abc");
    }

    [Fact]
    public async Task HeaderComOutraCaixa_DeveSerEncontrado()
    {
        // HTTP API entrega headers em minúsculo, mas não dependemos disso.
        var resposta = await Sut().HandlerAsync(Requisicao($"Bearer {TokenValido()}", "Authorization"), _contexto);
        resposta.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task SemHeader_DeveNegarSemContexto()
    {
        var resposta = await Sut().HandlerAsync(Requisicao(null), _contexto);

        resposta.IsAuthorized.Should().BeFalse();
        resposta.Context.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task TokenInvalido_DeveNegar()
    {
        var resposta = await Sut().HandlerAsync(Requisicao("Bearer lixo"), _contexto);
        resposta.IsAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task Segredo_DeveSerLidoUmaVezPorProcesso()
    {
        var sut = Sut();
        await sut.HandlerAsync(Requisicao($"Bearer {TokenValido()}"), _contexto);
        await sut.HandlerAsync(Requisicao($"Bearer {TokenValido()}"), _contexto);

        _sm.Verify(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FalhaAoLerSegredo_DevePropagar()
    {
        // Sem segredo não há como validar: falhar alto (500 no gateway) é melhor que negar
        // silenciosamente e mascarar o problema de infraestrutura.
        _sm.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new ResourceNotFoundException("nao existe"));

        var act = () => Sut().HandlerAsync(Requisicao($"Bearer {TokenValido()}"), _contexto);

        await act.Should().ThrowAsync<ResourceNotFoundException>();
    }
}
