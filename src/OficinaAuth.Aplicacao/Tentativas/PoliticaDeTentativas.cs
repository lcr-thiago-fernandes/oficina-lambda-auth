namespace OficinaAuth.Aplicacao.Tentativas;

/// <summary>
/// Limites de falhas por janela. "Origem" é o IP de quem chama (protege contra
/// enumeração de CPFs e força bruta distribuída sobre vários usuários); "identidade"
/// é o username (protege uma conta específica contra tentativas de várias origens).
/// </summary>
public sealed record PoliticaDeTentativas(int MaximoPorOrigem, int MaximoPorIdentidade, TimeSpan Janela)
{
    public static readonly PoliticaDeTentativas Padrao = new(10, 5, TimeSpan.FromMinutes(15));
}
