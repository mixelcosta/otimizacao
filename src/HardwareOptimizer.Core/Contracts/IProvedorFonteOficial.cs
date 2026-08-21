namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Fronteira única de consulta de versão "oficial" mais recente para um item
/// (driver, BIOS, software, etc.). Nenhum consumidor (RoteadorIpc, ViewModels,
/// AtualizadorDrivers) consulta fontes de dado específicas (ex.:
/// <c>IRepositorioDriversWhql</c>) diretamente — sempre passam por esta interface.
/// Vive em Core (não em Features.Atualizacao, onde a implementação concreta mora)
/// para evitar referência circular entre Features.Drivers e Features.Atualizacao.
///
/// Implementação de driver (<c>ProvedorFonteOficialDriver</c>, em
/// Features.Atualizacao) delega para o catálogo estático já existente. Consulta
/// HTTP real a fabricantes (NVIDIA, AMD, Intel, Realtek...) é trabalho futuro,
/// não-bloqueante nesta história (PRD §10 item 3) — a interface fica pronta para
/// recebê-la sem quebrar quem já a consome.
/// </summary>
public interface IProvedorFonteOficial
{
    /// <summary>
    /// Consulta a informação mais recente conhecida para o identificador informado
    /// (ex.: Hardware ID de um driver). Retorna <see langword="null"/> quando a
    /// fonte não tem informação disponível — nunca lança para esse caso.
    /// </summary>
    Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default);
}

/// <summary>
/// Resultado de uma consulta a <see cref="IProvedorFonteOficial"/>. Campos além
/// da versão (URL de download, certificação) existem para que consumidores não
/// precisem voltar a consultar a fonte de dado específica por trás da fronteira.
/// </summary>
public sealed record InfoFonteOficial
{
    public string? VersaoDisponivel { get; init; }
    public string? UrlDownload { get; init; }
    public bool CertificadoWhql { get; init; }
}
