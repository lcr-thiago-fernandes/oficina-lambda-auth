namespace OficinaAuth.Dominio;

/// <summary>Projeção de <c>clientes.cliente</c> com o mínimo necessário para autenticar.</summary>
public sealed record ClienteAutenticavel(Guid Id, string Nome, string Documento, bool Ativo);
