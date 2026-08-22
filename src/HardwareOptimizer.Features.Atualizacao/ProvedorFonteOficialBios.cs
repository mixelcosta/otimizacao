using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Implementação de <see cref="IProvedorFonteOficial"/> para a severidade
/// "bios": delega para o banco curado já existente (<see cref="IProvedorInfoBios"/>
/// — hoje, <see cref="BancoCuradoBios"/>). Mesmo padrão exato de
/// <c>ProvedorFonteOficialSoftware</c>/<c>ProvedorFonteOficialDriver</c> — fecha a
/// fronteira única (AD-4) também para BIOS: nenhum consumidor de BIOS fora desta
/// classe deve acessar <see cref="IProvedorInfoBios"/>/<see cref="BancoCuradoBios"/>
/// diretamente (Boundaries §Always da spec-1-4).
/// </summary>
public sealed class ProvedorFonteOficialBios : IProvedorFonteOficial
{
    private readonly IProvedorInfoBios _provedor;

    public ProvedorFonteOficialBios(IProvedorInfoBios provedor)
    {
        _provedor = provedor;
    }

    public async Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default)
    {
        var info = await _provedor.ObterAsync(identificador, ct).ConfigureAwait(false);
        if (info is null) return null;

        return new InfoFonteOficial
        {
            VersaoDisponivel = info.VersaoMaisRecente,
            // Fonte (não LinkManual) é o link específico da página de suporte/BIOS
            // do modelo exato — mais apropriado como "onde baixar" que o link
            // genérico de manual.
            UrlDownload = info.Fonte,
            CertificadoWhql = false, // não se aplica a BIOS
        };
    }
}
