namespace HardwareOptimizer.Core.Contracts;

public enum TipoPecaUpgrade
{
    Cpu = 0,
    Gpu = 1,
    Ram = 2,
    SsdM2 = 3,
    Fonte = 4,
}

public enum ModoSugestao
{
    CustoBeneficio = 0,
    HighEnd = 1,
}

public sealed record PecaSubstituta
{
    public required TipoPecaUpgrade Tipo { get; init; }

    public required string Modelo { get; init; }

    public string? Fabricante { get; init; }

    public string? Socket { get; init; }

    public string? TipoDdr { get; init; }

    public int? VelocidadeMhz { get; init; }

    public int? TdpW { get; init; }

    public string? InterfacePcie { get; init; }

    public decimal? PrecoEstimado { get; init; }
}

public sealed record ResultadoCompatibilidade
{
    public required bool Compativel { get; init; }

    public required string PecaModelo { get; init; }

    public IReadOnlyList<string> Incompatibilidades { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

    public double? GanhoEstimadoPercent { get; init; }

    public int? ConsumoTotalEstimadoW { get; init; }

    public int? HeadroomFonteW { get; init; }
}

public sealed record SugestaoUpgrade
{
    public required ModoSugestao Modo { get; init; }

    public required PecaSubstituta Peca { get; init; }

    public required ResultadoCompatibilidade Compatibilidade { get; init; }

    public required string Justificativa { get; init; }

    public string? UrlAfiliado { get; init; }
}

public sealed record GargaloResult
{
    public required string ComponenteLimitante { get; init; }

    public required double PorcentagemGargalo { get; init; }

    public required double GanhoEstimadoPercent { get; init; }

    public required string Descricao { get; init; }
}
