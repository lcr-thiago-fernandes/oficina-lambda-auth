using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao.Portas;

public interface IClienteRepositorio
{
    /// <param name="documento">Só dígitos, já validado por <see cref="Documento.Criar"/>.</param>
    Task<ClienteAutenticavel?> ObterPorDocumentoAsync(string documento, CancellationToken ct);
}
