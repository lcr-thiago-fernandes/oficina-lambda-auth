namespace OficinaAuth.Aplicacao.Portas;

/// <summary>
/// Contador de falhas de autenticação por chave, com janela fixa que começa na
/// primeira falha. Implementações: memória (local/testes) e DynamoDB (Lambda).
/// </summary>
public interface ILimitadorDeTentativas
{
    Task<int> ContarFalhasAsync(string chave, CancellationToken ct);
    Task RegistrarFalhaAsync(string chave, TimeSpan janela, CancellationToken ct);
    Task LimparAsync(string chave, CancellationToken ct);
}
