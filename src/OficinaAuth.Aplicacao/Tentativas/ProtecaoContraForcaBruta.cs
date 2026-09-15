using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Aplicacao.Tentativas;

public sealed class ProtecaoContraForcaBruta
{
    private readonly ILimitadorDeTentativas _limitador;
    private readonly PoliticaDeTentativas _politica;

    public ProtecaoContraForcaBruta(ILimitadorDeTentativas limitador, PoliticaDeTentativas politica)
    {
        _limitador = limitador;
        _politica = politica;
    }

    private static string ChaveOrigem(string origem) => $"origem:{origem}";
    private static string ChaveIdentidade(string identidade) => $"identidade:{identidade.Trim().ToLowerInvariant()}";

    public async Task ExigirPermitidoAsync(string origem, string? identidade, CancellationToken ct)
    {
        if (await _limitador.ContarFalhasAsync(ChaveOrigem(origem), ct) >= _politica.MaximoPorOrigem)
            throw new MuitasTentativasException(_politica.Janela);

        if (identidade is not null &&
            await _limitador.ContarFalhasAsync(ChaveIdentidade(identidade), ct) >= _politica.MaximoPorIdentidade)
            throw new MuitasTentativasException(_politica.Janela);
    }

    public async Task RegistrarFalhaAsync(string origem, string? identidade, CancellationToken ct)
    {
        await _limitador.RegistrarFalhaAsync(ChaveOrigem(origem), _politica.Janela, ct);
        if (identidade is not null)
            await _limitador.RegistrarFalhaAsync(ChaveIdentidade(identidade), _politica.Janela, ct);
    }

    /// <summary>Sucesso limpa só a identidade: a origem continua contando falhas (enumeração).</summary>
    public Task RegistrarSucessoAsync(string origem, string? identidade, CancellationToken ct) =>
        identidade is null ? Task.CompletedTask : _limitador.LimparAsync(ChaveIdentidade(identidade), ct);
}
