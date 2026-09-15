using OficinaAuth.Aplicacao.Portas;
using OficinaAuth.Aplicacao.Tentativas;
using OficinaAuth.Aplicacao.Tokens;
using OficinaAuth.Dominio;

namespace OficinaAuth.Aplicacao;

/// <summary>
/// Os três caminhos de erro são os três verbos do enunciado: validar o CPF (400),
/// consultar a existência (404), consultar o status (403).
/// </summary>
public sealed class AutenticarClienteUseCase
{
    private readonly IClienteRepositorio _clientes;
    private readonly EmissorDeToken _emissor;
    private readonly ProtecaoContraForcaBruta _protecao;

    public AutenticarClienteUseCase(IClienteRepositorio clientes, EmissorDeToken emissor, ProtecaoContraForcaBruta protecao)
    {
        _clientes = clientes;
        _emissor = emissor;
        _protecao = protecao;
    }

    public async Task<TokenEmitido> ExecutarAsync(AutenticarClienteRequest request, string? origem, CancellationToken ct)
    {
        // 1. Validar o CPF — antes de qualquer I/O, sem gastar tentativa nem banco.
        var documento = Documento.Criar(request.Cpf ?? string.Empty);

        // Sem identidade aqui: o CPF é semi-público e a ameaça real é enumeração,
        // que se controla pela origem.
        await _protecao.ExigirPermitidoAsync(origem, identidade: null, ct);

        // 2. Consultar a existência.
        var cliente = await _clientes.ObterPorDocumentoAsync(documento.Valor, ct);
        if (cliente is null)
        {
            await _protecao.RegistrarFalhaAsync(origem, null, ct);
            throw new ClienteNaoEncontradoException();
        }

        // 3. Consultar o status.
        if (!cliente.Ativo)
        {
            await _protecao.RegistrarFalhaAsync(origem, null, ct);
            throw new ClienteInativoException();
        }

        return await _emissor.EmitirAsync(cliente.Id, Perfis.Cliente, cliente.Nome, cliente.Documento, ct);
    }
}
