using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Infraestrutura;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}
