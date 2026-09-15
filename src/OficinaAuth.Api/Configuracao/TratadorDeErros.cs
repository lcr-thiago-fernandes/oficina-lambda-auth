using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OficinaAuth.Aplicacao;
using OficinaAuth.Dominio;

namespace OficinaAuth.Api.Configuracao;

/// <summary>
/// Traduz exceções em ProblemDetails. Exceções de domínio/aplicação viram 4xx com a
/// própria mensagem; qualquer outra vira 500 genérico (nunca vaza detalhe interno).
/// </summary>
public sealed class TratadorDeErros : IExceptionHandler
{
    private readonly ILogger<TratadorDeErros> _log;

    public TratadorDeErros(ILogger<TratadorDeErros> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        var (status, titulo, detalhe) = excecao switch
        {
            DocumentoInvalidoException e => (StatusCodes.Status400BadRequest, "Documento inválido", e.Message),
            ArgumentException e => (StatusCodes.Status400BadRequest, "Requisição inválida", e.Message),
            BadHttpRequestException e => (StatusCodes.Status400BadRequest, "Requisição inválida", e.Message),
            CredenciaisInvalidasException e => (StatusCodes.Status401Unauthorized, "Não autenticado", e.Message),
            ClienteInativoException e => (StatusCodes.Status403Forbidden, "Acesso negado", e.Message),
            UsuarioInativoException e => (StatusCodes.Status403Forbidden, "Acesso negado", e.Message),
            ClienteNaoEncontradoException e => (StatusCodes.Status404NotFound, "Não encontrado", e.Message),
            MuitasTentativasException e => (StatusCodes.Status429TooManyRequests, "Muitas tentativas", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Falha ao processar a autenticação.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _log.LogError(excecao, "Falha não tratada em {Rota}", http.Request.Path);
        else
            _log.LogInformation("Autenticação recusada com {Status} em {Rota}: {Motivo}", status, http.Request.Path, detalhe);

        if (excecao is MuitasTentativasException m)
            http.Response.Headers.RetryAfter = ((int)m.RetryAfter.TotalSeconds).ToString();

        if (status == StatusCodes.Status401Unauthorized)
            http.Response.Headers.WWWAuthenticate = "Bearer";

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = titulo,
            Status = status,
            Detail = detalhe,
            Instance = http.Request.Path
        }, options: null, contentType: "application/problem+json", ct);

        return true;
    }
}
