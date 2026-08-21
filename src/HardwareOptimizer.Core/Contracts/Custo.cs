namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Custo estimado de uma ação de atualização/otimização. Contrato compartilhado —
/// primeiro consumo real fora desta história é a Story 3.8 (espinha de arquitetura).
/// </summary>
public sealed record Custo
{
    public required decimal ValorEstimado { get; init; }

    public string Moeda { get; init; } = "BRL";
}
