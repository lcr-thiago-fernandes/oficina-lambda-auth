namespace OficinaAuth.Aplicacao.Tokens;

public sealed record TokenEmitido(string AccessToken, string TokenType, int ExpiresIn, string Perfil);
