namespace HardwareOptimizer.Core.Contracts;

public enum NivelSaudeDisco
{
    Bom = 0,
    Atencao = 1,
    Critico = 2,
}

public sealed record SaudeDisco
{
    public required string Modelo { get; init; }

    public required string Letra { get; init; }

    public long TbwEscritoGb { get; init; }

    /// <summary>TBW máximo garantido pelo fabricante para este modelo. Zero = desconhecido.</summary>
    public long TbwFabricanteGb { get; init; }

    public int HorasUso { get; init; }

    public double PorcentagemVidaRestante { get; init; }

    public NivelSaudeDisco Nivel { get; init; }

    public bool TemErrosNaoCorrigiveis { get; init; }

    public int SetoresComProblema { get; init; }

    public DateTimeOffset AtualizadoEm { get; init; } = DateTimeOffset.UtcNow;
}
