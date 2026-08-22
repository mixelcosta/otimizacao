using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Consulta a versão "oficial" mais recente conhecida para um programa
/// instalado, por nome. Mesmo padrão de <c>IRepositorioDriversWhql</c>
/// (Features.Drivers). MVP: implementação offline com catálogo estático
/// (<see cref="RepositorioVersoesSoftwareEstatico"/>); produção: integração real
/// com lojas/sites de cada fabricante — sem solução unificada hoje (PRD §10
/// item 3), portanto trabalho futuro não-bloqueante.
/// </summary>
public interface IRepositorioVersoesSoftware
{
    /// <summary>
    /// Retorna <see langword="null"/> quando o catálogo não tem cobertura para o
    /// nome informado — nunca lança para esse caso.
    /// </summary>
    Task<InfoSoftware?> ConsultarAsync(string nomePrograma, CancellationToken ct = default);
}
