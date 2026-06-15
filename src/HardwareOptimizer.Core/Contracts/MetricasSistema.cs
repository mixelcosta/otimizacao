namespace HardwareOptimizer.Core.Contracts;

/// <summary>Snapshot de métricas de uso do sistema no momento do SCAN.</summary>
public sealed record MetricasSistema
{
    /// <summary>Uso médio de CPU em % no momento da coleta.</summary>
    public int CpuUsoPercent { get; init; }

    public long RamTotalMb { get; init; }

    public long RamLivreMb { get; init; }

    public long RamUsadaMb => RamTotalMb - RamLivreMb;

    public IReadOnlyList<MetricaDisco> Discos { get; init; } = Array.Empty<MetricaDisco>();
}

public sealed record MetricaDisco
{
    /// <summary>Ex: "C:", "D:".</summary>
    public required string Letra { get; init; }

    public long TotalGb { get; init; }

    public long LivreGb { get; init; }

    public long UsadoGb { get; init; }

    public int UsoPercent => TotalGb > 0 ? (int)(UsadoGb * 100 / TotalGb) : 0;
}
