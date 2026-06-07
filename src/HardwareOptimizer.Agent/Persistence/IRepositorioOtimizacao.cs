using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Persistence;

/// <summary>
/// Persistência local (SQLite): inventário, auditoria de consentimento e
/// histórico de execução. A auditoria de consentimento é exigência do documento
/// para rastreabilidade.
/// </summary>
public interface IRepositorioOtimizacao
{
    Task InicializarAsync(CancellationToken cancellationToken = default);

    Task<long> SalvarInventarioAsync(Inventario inventario, CancellationToken cancellationToken = default);

    Task<long> RegistrarConsentimentoAsync(
        RegistroConsentimento registro, CancellationToken cancellationToken = default);

    Task<long> RegistrarExecucaoAsync(
        RelatorioExecucao relatorio, CancellationToken cancellationToken = default);

    Task<long> ContarInventariosAsync(CancellationToken cancellationToken = default);

    Task<long> ContarConsentimentosAsync(CancellationToken cancellationToken = default);

    Task<long> ContarExecucoesAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera o JSON de informação de BIOS cacheado para a chave, ou nulo.</summary>
    Task<string?> ObterCacheBiosAsync(string chaveBusca, CancellationToken cancellationToken = default);

    Task SalvarCacheBiosAsync(string chaveBusca, string dadosJson, CancellationToken cancellationToken = default);
}
