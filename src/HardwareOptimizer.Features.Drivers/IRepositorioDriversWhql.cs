using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Drivers;

/// <summary>
/// Consulta a base de dados WHQL para um Hardware ID.
/// MVP: implementação offline com catálogo estático; produção: REST API.
/// </summary>
public interface IRepositorioDriversWhql
{
    Task<InfoDriver?> ConsultarAsync(string hardwareId, CancellationToken ct = default);
}
