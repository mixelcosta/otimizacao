namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Ganho estimado de uma ação de atualização/otimização, com margem de confiança
/// declarada. Contrato compartilhado — primeiro consumo real fora desta história
/// é a Story 3.4/3.5 (espinha de arquitetura).
/// </summary>
public sealed record GanhoEstimado
{
    public required double Percentual { get; init; }

    public required double MargemConfianca { get; init; }

    public DateTimeOffset AtualizadoEm { get; init; } = DateTimeOffset.UtcNow;
}
