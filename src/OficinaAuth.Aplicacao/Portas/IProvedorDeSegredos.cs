namespace OficinaAuth.Aplicacao.Portas;

/// <summary>Lê um segredo pelo nome do Secrets Manager (ex.: <c>oficina/jwt_secret</c>).</summary>
public interface IProvedorDeSegredos
{
    Task<string> ObterAsync(string nome, CancellationToken ct);
}
