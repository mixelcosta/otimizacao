namespace HardwareOptimizer.Core.Reporting;

/// <summary>Resumo de uma alteração aplicada (antes/depois), neutro de plataforma.</summary>
public sealed record AlteracaoResumo(string Alvo, string? Antes, string? Depois);

/// <summary>Conjunto de notas por domínio + nota final consolidada.</summary>
public sealed record ResultadoScore
{
    public required IReadOnlyList<Score> Scores { get; init; }

    /// <summary>Nota final 0-100 (média ponderada dos domínios primários).</summary>
    public required int NotaFinal { get; init; }

    public Score? Obter(Dominio dominio) => Scores.FirstOrDefault(s => s.Dominio == dominio);
}

/// <summary>
/// Relatório executivo final: resumo, notas por domínio, nota final 0-100,
/// destaques e o antes/depois das alterações (contrato da Fase 10).
/// </summary>
public sealed record RelatorioExecutivo
{
    public DateTimeOffset GeradoEm { get; init; } = DateTimeOffset.UtcNow;

    public required string ResumoExecutivo { get; init; }

    public required int NotaFinal { get; init; }

    public required string Classificacao { get; init; }

    public required IReadOnlyList<Score> Scores { get; init; }

    public IReadOnlyList<AlteracaoResumo> Alteracoes { get; init; } = Array.Empty<AlteracaoResumo>();

    public IReadOnlyList<string> Destaques { get; init; } = Array.Empty<string>();

    public bool RegressaoDetectada { get; init; }
}
