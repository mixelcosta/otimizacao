namespace HardwareOptimizer.Core.Contracts;

/// <summary>Resumo dos arquivos temporários encontrados no sistema.</summary>
public sealed record AnaliseTemp
{
    /// <summary>Espaço total ocupado por arquivos temporários em MB.</summary>
    public long TamanhoTotalMb { get; init; }

    public int QuantidadeArquivos { get; init; }

    public IReadOnlyList<PastaTemp> Pastas { get; init; } = Array.Empty<PastaTemp>();
}

public sealed record PastaTemp
{
    public required string Caminho { get; init; }

    public long TamanhoMb { get; init; }

    public int QuantidadeArquivos { get; init; }
}
