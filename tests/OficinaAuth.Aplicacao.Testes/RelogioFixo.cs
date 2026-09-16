using OficinaAuth.Aplicacao.Portas;

namespace OficinaAuth.Aplicacao.Testes;

public sealed class RelogioFixo : IRelogio
{
    // DESVIO do brief: a literal original `new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)` coincide
    // com o "hoje" deste ambiente e já fica no passado em relação ao relógio real da máquina
    // (verificado: `date -u` mostrou 2026-09-15 14:1x UTC durante a implementação). Como o
    // teste de contrato valida o token com JsonWebTokenHandler.ValidateTokenAsync usando o
    // relógio REAL do sistema (TokenValidationParameters não tem override de clock aqui, de
    // propósito, pois são cópia exata dos parâmetros da API), qualquer valor absoluto fixo
    // no passado faz `exp` (Agora+60min) cair antes do "agora" real, e a validação rejeita o
    // token por expiração — não é falha do EmissorDeToken. Usar DateTimeOffset.UtcNow mantém
    // o propósito do fixture (um instante único, estável durante a vida do teste, usado nas
    // asserções de IssuedAt/ValidTo) sem depender de uma data literal que pode ficar obsoleta.
    // O JWT NumericDate (RFC 7519) só tem resolução de segundos: truncar para o segundo
    // cheio evita que a fração de segundo de UtcNow quebre as asserções de igualdade exata
    // (jwt.IssuedAt/jwt.ValidTo) depois do round-trip pelo token.
    public DateTimeOffset Agora { get; set; } = TruncarParaSegundo(DateTimeOffset.UtcNow);
    public void Avancar(TimeSpan delta) => Agora = Agora.Add(delta);

    private static DateTimeOffset TruncarParaSegundo(DateTimeOffset instante) =>
        instante.AddTicks(-(instante.Ticks % TimeSpan.TicksPerSecond));
}
