using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Drivers;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Implementação de <see cref="IProvedorFonteOficial"/> para a severidade
/// "driver": delega para o catálogo WHQL já existente
/// (<see cref="IRepositorioDriversWhql"/> — hoje, <c>RepositorioWhqlEstatico</c>).
/// Não faz nenhuma chamada HTTP real a fabricante — isso é trabalho futuro,
/// registrado como item não-bloqueante na espinha de arquitetura.
/// </summary>
public sealed class ProvedorFonteOficialDriver : IProvedorFonteOficial
{
    private readonly IRepositorioDriversWhql _repositorio;

    public ProvedorFonteOficialDriver(IRepositorioDriversWhql repositorio)
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
            CertificadoWhql = info.CertificadoWhql,
        };
    }
}
