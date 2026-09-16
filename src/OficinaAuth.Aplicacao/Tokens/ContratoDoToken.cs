namespace OficinaAuth.Aplicacao.Tokens;

/// <summary>
/// Contrato do JWT compartilhado com o oficina-app (que só VALIDA). Qualquer mudança
/// aqui precisa da mudança correspondente em ConfiguracaoJwt.cs / JwtOptions.cs lá.
/// </summary>
public static class ContratoDoToken
{
    public const string Emissor = "oficina-auth";
    public const string Audiencia = "oficina-api";
    public const string ClaimPerfil = "perfil";
    public const string ClaimDocumento = "documento";
    public const string ClaimNome = "nome";
    public const int TamanhoMinimoDoSegredo = 32;
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(60);
}
