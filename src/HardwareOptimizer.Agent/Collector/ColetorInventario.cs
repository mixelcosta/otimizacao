using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Coletor read-only que delega ao leitor da plataforma corrente. Seleciona
/// automaticamente o leitor adequado (Windows/Linux) quando nenhum é informado.
/// </summary>
public sealed class ColetorInventario : IColetorInventario
{
    private readonly ILeitorPlataforma _leitor;

    public ColetorInventario(ILeitorPlataforma? leitor = null)
    {
        _leitor = leitor ?? CriarLeitorPadrao();
    }

    public Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default) =>
        _leitor.LerAsync(cancellationToken);

    private static ILeitorPlataforma CriarLeitorPadrao() =>
        OperatingSystem.IsWindows() ? new LeitorWindows() : (ILeitorPlataforma)new LeitorLinux();
}
