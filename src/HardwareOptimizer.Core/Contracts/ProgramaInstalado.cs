namespace HardwareOptimizer.Core.Contracts;

public sealed record ProgramaInstalado
{
    public required string Nome { get; init; }

    public string? Versao { get; init; }

    public string? Fabricante { get; init; }

    /// <summary>Data no formato YYYYMMDD vinda do registro.</summary>
    public string? DataInstalacao { get; init; }

    /// <summary>Tamanho estimado em MB (campo EstimatedSize do registro, originalmente em KB).</summary>
    public int? TamanhoMb { get; init; }

    /// <summary>Heurística: nome coincide com padrões conhecidos de bloatware/adware.</summary>
    public bool Bloatware { get; init; }
}
