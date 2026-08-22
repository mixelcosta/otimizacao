using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Ponto único de comparação de versão de software instalado
/// (Boundaries §Never da spec-1-3 — deliberadamente não estende
/// <see cref="OrquestradorAtualizacao"/>, que é acoplado a driver
/// via backup/instalação/rollback por pnputil, o que não se aplica aqui).
///
/// Para cada programa em <c>Inventario.ProgramasInstalados</c>, consulta
/// <see cref="IProvedorFonteOficial"/> — mesma lógica de comparação de versão de
/// <c>AtualizadorDrivers.VarrerAsync</c> — mas, diferente do driver, um item sem
/// cobertura ou com a mesma versão nunca é incluído no resultado: esta lista já
/// é "os desatualizados", não um inventário completo de status (guard
/// anti-alucinação, Boundaries §Always/§Never).
/// </summary>
public sealed class VerificadorSoftware
{
    private readonly IProvedorFonteOficial _provedor;
    private readonly ILogger<VerificadorSoftware> _log;

    public VerificadorSoftware(IProvedorFonteOficial provedor, ILogger<VerificadorSoftware> log)
    {
        _provedor = provedor;
        _log = log;
    }

    public async Task<IReadOnlyList<InfoSoftware>> VerificarAsync(
        IReadOnlyList<ProgramaInstalado> programas, CancellationToken ct = default)
    {
        var resultado = new List<InfoSoftware>();

        foreach (var programa in programas)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (string.IsNullOrEmpty(programa.Versao))
                    continue; // versão instalada não lida — não afirmar desatualização sem essa base (guard anti-alucinação)

                var oficial = await _provedor.ConsultarAsync(programa.Nome, ct).ConfigureAwait(false);
                if (oficial is null || string.IsNullOrEmpty(oficial.VersaoDisponivel))
                    continue; // sem cobertura — nunca aparece com dado inventado

                bool desatualizado = oficial.VersaoDisponivel != programa.Versao;
                if (!desatualizado)
                    continue; // mesma versão — não aparece na lista de desatualizados

                resultado.Add(new InfoSoftware
                {
                    Nome = programa.Nome,
                    VersaoAtual = programa.Versao,
                    VersaoDisponivel = oficial.VersaoDisponivel,
                    UrlDownload = oficial.UrlDownload,
                    Status = StatusSoftware.AtualizacaoDisponivel,
                });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Falha ao consultar fonte oficial para o programa '{Nome}'.", programa.Nome);
                // tratado como sem cobertura — não aparece, nunca propaga (I/O Matrix)
            }
        }

        return resultado;
    }
}
