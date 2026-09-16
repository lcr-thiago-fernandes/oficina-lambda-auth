using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao;

/// <summary>429 — o chamador excedeu as tentativas da janela.</summary>
public sealed class MuitasTentativasException : ExcecaoDeDominio
{
    public TimeSpan RetryAfter { get; }

    public MuitasTentativasException(TimeSpan retryAfter)
        : base("Muitas tentativas de autenticação. Tente novamente mais tarde.")
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>404 — CPF válido, mas não há cliente cadastrado com ele.</summary>
public sealed class ClienteNaoEncontradoException : ExcecaoDeDominio
{
    public ClienteNaoEncontradoException() : base("Cliente não encontrado.") { }
}

/// <summary>403 — cliente existe, mas está inativo.</summary>
public sealed class ClienteInativoException : ExcecaoDeDominio
{
    public ClienteInativoException() : base("Cliente inativo.") { }
}

/// <summary>401 — usuário inexistente OU senha errada (mesma resposta de propósito).</summary>
public sealed class CredenciaisInvalidasException : ExcecaoDeDominio
{
    public CredenciaisInvalidasException() : base("Usuário ou senha inválidos.") { }
}

/// <summary>403 — usuário administrativo existe e a senha confere, mas está inativo.</summary>
public sealed class UsuarioInativoException : ExcecaoDeDominio
{
    public UsuarioInativoException() : base("Usuário inativo.") { }
}

/// <summary>500 — auth.usuario.perfil traz um valor fora de {Admin, Atendente}. Dado corrompido, não erro do chamador.</summary>
public sealed class PerfilDesconhecidoException : InvalidOperationException
{
    public PerfilDesconhecidoException(string perfil)
        : base($"Perfil '{perfil}' em auth.usuario não pertence ao vocabulário do token.") { }
}
