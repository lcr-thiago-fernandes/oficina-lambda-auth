namespace OficinaAuth.Aplicacao.Portas;

/// <summary>
/// Identificadores no AWS Secrets Manager. Contrato com oficina-app
/// (docs/contratos-entre-repositorios.md, seção 3): valores em texto puro.
/// </summary>
public static class NomesDeSegredos
{
    public const string Jwt = "oficina/jwt_secret";
    public const string SenhaDoBanco = "oficina/db_password";
}
