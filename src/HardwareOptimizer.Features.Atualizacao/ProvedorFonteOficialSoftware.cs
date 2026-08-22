using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Implementação de <see cref="IProvedorFonteOficial"/> para a severidade
/// "software": delega para o catálogo estático de versões
/// (<see cref="IRepositorioVersoesSoftware"/> — hoje, <c>RepositorioVersoesSoftwareEstatico</c>).
/// Mesmo padrão exato de <c>ProvedorFonteOficialDriver</c>. Não faz nenhuma
/// chamada HTTP real a lojas/sites de fabricante — isso é trabalho futuro,
/// não-bloqueante nesta história (PRD §10 item 3).
/// </summary>
public sealed class ProvedorFonteOficialSoftware : IProvedorFonteOficial
{
    private readonly IRepositorioVersoesSoftware _repositorio;

    public ProvedorFonteOficialSoftware(IRepositorioVersoesSoftware repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default)
    {
        var info = await _repositorio.ConsultarAsync(identificador, ct).ConfigureAwait(false);
        if (info is null) return null;

        return new InfoFonteOficial
        {
            VersaoDisponivel = info.VersaoDisponivel,
            UrlDownload = info.UrlDownload,
            CertificadoWhql = false, // não se aplica a software
        };
    }
}
