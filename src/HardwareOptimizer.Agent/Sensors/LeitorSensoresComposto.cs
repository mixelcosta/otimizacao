using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Encadeia leitores de sensores e devolve a primeira leitura **com dados**,
/// permitindo degradação graciosa: no Windows tenta o LibreHardwareMonitor (rico,
/// requer driver/elevação) e, se vier vazio, recai sobre o WMI (temperatura, sem
/// elevação). Read-only, como todos os leitores.
/// </summary>
public sealed class LeitorSensoresComposto : ILeitorSensores
{
    private readonly IReadOnlyList<ILeitorSensores> _leitores;
    private readonly ILogger _log;

    public LeitorSensoresComposto(IReadOnlyList<ILeitorSensores> leitores, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(leitores);
        if (leitores.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um leitor.", nameof(leitores));
        }

        _leitores = leitores;
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => _leitores[0].Tipo;

    public async Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        foreach (var leitor in _leitores)
        {
            var leitura = await leitor.LerAsync(cancellationToken).ConfigureAwait(false);
            if (leitura.Sensores.Count > 0)
            {
                return leitura;
            }

            _log.LogDebug("Leitor {Leitor} sem dados; tentando o próximo.", leitor.GetType().Name);
        }

        _log.LogWarning("Nenhum leitor de sensores retornou dados.");
        return new LeituraSensores();
    }
}
