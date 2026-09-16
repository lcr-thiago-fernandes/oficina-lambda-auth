namespace OficinaAuth.Dominio;

/// <summary>
/// Raiz das exceções de regra de negócio. Serve para a API distinguir erro do
/// chamador (4xx) de falha de processamento (5xx), como no oficina-app.
/// </summary>
public abstract class ExcecaoDeDominio : Exception
{
    protected ExcecaoDeDominio(string mensagem) : base(mensagem) { }
}

public sealed class DocumentoInvalidoException : ExcecaoDeDominio
{
    public DocumentoInvalidoException(string mensagem) : base(mensagem) { }
}
