using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário específico de plataforma. Toda implementação é
/// estritamente read-only: jamais modifica o sistema.
/// </summary>
public interface ILeitorPlataforma
{
    SistemaOperacionalTipo Tipo { get; }

    Task<Inventario> LerAsync(CancellationToken cancellationToken = default);
}

/// <summary>Orquestrador do coletor de inventário.</summary>
public interface IColetorInventario
{
    Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default);
}
