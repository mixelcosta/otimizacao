namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Métricas extraídas da saída de uma ferramenta de estresse (OCCT, Prime95,
/// Cinebench, MemTest86). Inclui os sinais de falha que indicam regressão.
/// </summary>
public sealed record MedicaoEstresse
{
    public double? TempMaxC { get; init; }

    public double? ClockMhz { get; init; }

    public double? ConsumoW { get; init; }

    public double? Pontuacao { get; init; }

    public int ErrosWhea { get; init; }

    public int ErrosMemoria { get; init; }

    public bool Artefatos { get; init; }

    public bool DriverTimeout { get; init; }

    public bool TelaAzul { get; init; }

    /// <summary>Há falha crítica inequívoca (WHEA, memória, artefatos, TDR ou BSOD)?</summary>
    public bool TemFalhaCritica =>
        ErrosWhea > 0 || ErrosMemoria > 0 || Artefatos || DriverTimeout || TelaAzul;
}

/// <summary>Limiares que definem o que conta como regressão.</summary>
public sealed record LimiaresValidacao
{
    /// <summary>Acima desta temperatura máxima, considera-se regressão térmica.</summary>
    public double TempMaxAceitavelC { get; init; } = 95;

    /// <summary>Queda relativa de pontuação tolerada antes/depois (ex.: 0,05 = 5%).</summary>
    public double MargemQuedaPontuacao { get; init; } = 0.05;

    public static LimiaresValidacao Padrao { get; } = new();
}
