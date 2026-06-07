using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores específico de plataforma. Read-only: nunca modifica o
/// sistema. No Windows, a implementação de produção usa um driver de kernel
/// assinado (LibreHardwareMonitor) — atentar a Secure Boot.
/// </summary>
public interface ILeitorSensores
{
    SistemaOperacionalTipo Tipo { get; }

    Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default);
}
