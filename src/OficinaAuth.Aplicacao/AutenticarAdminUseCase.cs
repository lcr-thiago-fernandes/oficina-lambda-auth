using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tentativas;
using OficinaAuth.Aplicacao.Tokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao;

/// <summary>
/// Login de Admin/Atendente contra <c>auth.usuario</c>, tabela escrita pelo bootstrap
/// do oficina-app (hash BCrypt custo 12). Regras anti-enumeração:
/// usuário inexistente e senha errada dão o mesmo 401 e custam o mesmo tempo
/// (o BCrypt roda contra um hash fictício quando não há usuário);
/// "inativo" (403) só é revelado a quem apresentou a senha correta.
/// </summary>
public sealed class AutenticarAdminUseCase
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly EmissorDeToken _emissor;
    private readonly ProtecaoContraForcaBruta _protecao;

    public AutenticarAdminUseCase(IUsuarioRepositorio usuarios, EmissorDeToken emissor, ProtecaoContraForcaBruta protecao)
    {
        _usuarios = usuarios;
        _emissor = emissor;
        _protecao = protecao;
    }

    public async Task<TokenEmitido> ExecutarAsync(AutenticarAdminRequest request, string? origem, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            throw new ArgumentException("username e password são obrigatórios.");

        Username username;
        try
        {
            username = Username.Criar(request.Username);
        }
        catch (ArgumentException)
        {
            // Formato inválido nunca corresponde a um usuário gravado; responder 401 não
            // revela a regra de formato nem consome uma leitura no banco.
            throw new CredenciaisInvalidasException();
        }

        await _protecao.ExigirPermitidoAsync(origem, username.Valor, ct);

        var usuario = await _usuarios.ObterPorUsernameAsync(username.Valor, ct);

        // Tempo constante: verifica sempre, contra o hash real ou o fictício.
        var senhaConfere = VerificadorDeSenha.Confere(usuario?.PasswordHash ?? VerificadorDeSenha.HashFicticio, request.Password);

        if (usuario is null || !senhaConfere)
        {
            await _protecao.RegistrarFalhaAsync(origem, username.Valor, ct);
            throw new CredenciaisInvalidasException();
        }

        if (!usuario.Ativo)
            throw new UsuarioInativoException();

        if (!Perfis.EhAdministrativo(usuario.Perfil))
            throw new PerfilDesconhecidoException(usuario.Perfil);

        await _protecao.RegistrarSucessoAsync(origem, username.Valor, ct);

        return await _emissor.EmitirAsync(usuario.Id, usuario.Perfil, usuario.Username, documento: null, ct);
    }
}
