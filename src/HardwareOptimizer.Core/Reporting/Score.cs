namespace HardwareOptimizer.Core.Reporting;

/// <summary>Domínios pontuados no relatório executivo (do documento, campo "scores").</summary>
public enum Dominio
{
    Hardware = 0,
    Bios = 1,
    Cpu = 2,
    Gpu = 3,
    Ram = 4,
    Windows = 5,
    Estabilidade = 6,
}

/// <summary>
/// Nota de um domínio (0-100), com classificação legível e os critérios que a
/// compuseram, para transparência do cálculo.
/// </summary>
public sealed record Score
{
    public required Dominio Dominio { get; init; }

    /// <summary>Valor de 0 a 100.</summary>
    public required int Valor { get; init; }

    public required string Classificacao { get; init; }

    public IReadOnlyList<string> Criterios { get; init; } = Array.Empty<string>();

    /// <summary>Classifica uma nota 0-100 em faixa legível.</summary>
    public static string Classificar(int valor) => valor switch
    {
        >= 85 => "Excelente",
        >= 70 => "Bom",
        >= 50 => "Regular",
        _ => "Requer atenção",
    };
}
