namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Resultado de um teste de estresse comparando antes/depois (contrato
/// "resultado_validacao"). Alimenta a decisão de manter ou reverter a categoria.
/// </summary>
public sealed record ResultadoValidacao
{
    public required string Categoria { get; init; }

    public required string Ferramenta { get; init; }

    public MedicaoTeste? Antes { get; init; }

    public MedicaoTeste? Depois { get; init; }

    public bool Regressao { get; init; }

    public IReadOnlyList<string> Erros { get; init; } = Array.Empty<string>();

    /// <summary>Ex.: "Totalmente validado", "Validado com ressalvas", "Reprovado".</summary>
    public required string Estabilidade { get; init; }
}

public sealed record MedicaoTeste
{
    public double? Score { get; init; }

    public double? TempMaxC { get; init; }

    public double? ClockMhz { get; init; }

    public double? ConsumoW { get; init; }
}
