namespace OficinaAuth.Aplicacao;

/// <summary>Corpo de <c>POST /auth/cliente</c>. Aceita CPF com ou sem máscara (e CNPJ, para clientes PJ).</summary>
public sealed record AutenticarClienteRequest(string? Cpf);

/// <summary>Corpo de <c>POST /auth/admin</c>.</summary>
public sealed record AutenticarAdminRequest(string? Username, string? Password);
