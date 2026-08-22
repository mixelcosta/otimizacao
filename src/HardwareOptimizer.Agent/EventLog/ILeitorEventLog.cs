using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.EventLog;

/// <summary>
/// Leitor do Event Log do Windows específico de plataforma. Read-only: nunca
/// modifica o sistema. Interface fina (mesmo padrão de
/// <c>ILeitorSensores</c>/<c>ILeitorPlataforma</c>), testável via fake. Sempre
/// invocado sob demanda — nunca em timer/daemon/background (Boundaries §Always
/// da spec-1-5).
/// </summary>
public interface ILeitorEventLog
{
    Task<IReadOnlyList<EventoInstabilidade>> LerAsync(int diasRecentes, CancellationToken cancellationToken = default);
}
