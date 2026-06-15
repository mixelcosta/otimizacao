namespace HardwareOptimizer.Core.Contracts;

public sealed record ProcessoSistema
{
    public required string Nome { get; init; }

    public int Pid { get; init; }

    /// <summary>Memória de trabalho em KB (WorkingSet).</summary>
    public long MemoriaKb { get; init; }
}
