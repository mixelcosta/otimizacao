using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Ponto único de comparação de versão de BIOS através da fronteira fina
/// <see cref="IProvedorFonteOficial"/> (Boundaries §Never da spec-1-4 —
/// deliberadamente não estende <c>AnalisadorBios</c>/<c>ModuloBios</c>, que
/// comparam contra <c>InfoBiosFabricante</c>, incompatível com a fronteira fina).
///
/// Reaproveita <see cref="VersaoBios.EhMaisRecente"/> (comparação) e
/// <see cref="GeradorGuiaBios.Gerar"/> (guia passo a passo) — nenhuma lógica de
/// comparação/geração de guia é recriada. Mesmo guard anti-alucinação de
/// <c>VerificadorSoftware</c> (Story 1.3): sem cobertura no catálogo, ou já
/// atualizada, nunca aparece — nunca "sem informação" genérico.
/// </summary>
public sealed class VerificadorBios
{
    private readonly IProvedorFonteOficial _provedor;
    private readonly ILogger<VerificadorBios> _log;
    private readonly GeradorGuiaBios _gerador = new();

    public VerificadorBios(IProvedorFonteOficial provedor, ILogger<VerificadorBios> log)
    {
        _provedor = provedor;
        _log = log;
    }

    public async Task<InfoBios?> VerificarAsync(PlacaMae placa, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placa);

        try
        {
            var identificacao = IdentificacaoBios.DeInventario(placa);

            if (string.IsNullOrWhiteSpace(identificacao.VersaoAtual))
                return null; // versão instalada não lida — não afirmar desatualização sem essa base (guard anti-alucinação)

            var oficial = await _provedor.ConsultarAsync(identificacao.ChaveBusca, ct).ConfigureAwait(false);
            if (oficial is null || string.IsNullOrWhiteSpace(oficial.VersaoDisponivel))
                return null; // sem cobertura — nunca aparece com dado inventado

            var desatualizada = VersaoBios.EhMaisRecente(identificacao.VersaoAtual, oficial.VersaoDisponivel);
            if (!desatualizada)
                return null; // mesma versão (ou mais nova) — nenhum alerta

            var guia = _gerador.Gerar(identificacao);

            return new InfoBios
            {
                Fabricante = identificacao.Fabricante,
                Modelo = identificacao.Modelo,
                VersaoAtual = identificacao.VersaoAtual,
                VersaoDisponivel = oficial.VersaoDisponivel,
                UrlDownload = oficial.UrlDownload,
                TeclaSetup = guia.TeclaSetup,
                Utilitario = guia.Utilitario,
                Passos = guia.Passos,
                Avisos = guia.Avisos,
            };
        }
        catch (OperationCanceledException)
        {
            throw; // cancelamento nunca é "falha do provedor" — deve propagar, não ser tratado como sem cobertura
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Falha ao consultar fonte oficial de BIOS para '{Fabricante} {Modelo}'.",
                placa.Fabricante, placa.Modelo);
            return null; // tratado como sem cobertura — não aparece, nunca propaga (I/O Matrix)
        }
    }
}
