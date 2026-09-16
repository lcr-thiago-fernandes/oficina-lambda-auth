using System.Collections.Concurrent;
using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura.Tentativas;

/// <summary>
/// Contador em memória, para execução local e testes. Em Lambda cada container tem
/// a sua memória, então NÃO protege nada em produção — lá entra o DynamoDB.
/// </summary>
public sealed class LimitadorDeTentativasMemoria : ILimitadorDeTentativas
{
    private sealed record Registro(int Falhas, DateTimeOffset ExpiraEm);

    private readonly ConcurrentDictionary<string, Registro> _registros = new();
    private readonly IRelogio _relogio;

    public LimitadorDeTentativasMemoria(IRelogio relogio) => _relogio = relogio;

    public Task<int> ContarFalhasAsync(string chave, CancellationToken ct)
    {
        if (!_registros.TryGetValue(chave, out var r) || r.ExpiraEm <= _relogio.Agora)
            return Task.FromResult(0);
        return Task.FromResult(r.Falhas);
    }

    public Task RegistrarFalhaAsync(string chave, TimeSpan janela, CancellationToken ct)
    {
        var agora = _relogio.Agora;
        _registros.AddOrUpdate(
            chave,
            _ => new Registro(1, agora.Add(janela)),
            (_, atual) => atual.ExpiraEm <= agora
                ? new Registro(1, agora.Add(janela))
                : atual with { Falhas = atual.Falhas + 1 });
        return Task.CompletedTask;
    }

    public Task LimparAsync(string chave, CancellationToken ct)
    {
        _registros.TryRemove(chave, out _);
        return Task.CompletedTask;
    }
}
