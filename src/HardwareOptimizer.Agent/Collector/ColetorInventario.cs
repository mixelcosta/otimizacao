using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Coletor read-only que delega ao leitor da plataforma corrente. Seleciona
/// automaticamente o leitor adequado (Windows/Linux) quando nenhum é informado.
/// </summary>
public sealed class ColetorInventario : IColetorInventario
{
    private readonly ILeitorPlataforma _leitor;
    private readonly ILogger _log;

    public ColetorInventario(ILeitorPlataforma? leitor = null, ILoggerFactory? loggerFactory = null)
    {
        var fabrica = loggerFactory ?? NullLoggerFactory.Instance;
        _log = fabrica.CreateLogger<ColetorInventario>();
        _leitor = leitor ?? CriarLeitorPadrao(fabrica);
    }

    public async Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default)
    {
        _log.LogInformation("Iniciando coleta de inventário (leitor {Plataforma}).", _leitor.Tipo);

        var inventario = await _leitor.LerAsync(cancellationToken).ConfigureAwait(false);

        _log.LogInformation(
            "Coleta concluída: placa '{Fabricante} {Modelo}', CPU '{Cpu}', {Memorias} módulo(s) de memória, {Gpus} GPU(s).",
            inventario.Placa.Fabricante, inventario.Placa.Modelo, inventario.Cpu.Nome,
            inventario.Memoria.Count, inventario.Gpu.Count);

        return inventario;
    }

    private static ILeitorPlataforma CriarLeitorPadrao(ILoggerFactory fabrica) =>
        OperatingSystem.IsWindows()
            ? new LeitorWindows(fabrica.CreateLogger<LeitorWindows>())
            : (ILeitorPlataforma)new LeitorLinux(fabrica.CreateLogger<LeitorLinux>());
}
