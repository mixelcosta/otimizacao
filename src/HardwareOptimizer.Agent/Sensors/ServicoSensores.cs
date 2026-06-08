using System.Globalization;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Serviço de sensores: delega ao leitor da plataforma corrente, escolhido
/// automaticamente quando nenhum é informado. Leitura em tempo real.
/// </summary>
public sealed class ServicoSensores
{
    private readonly ILeitorSensores _leitor;
    private readonly ILogger _log;

    public ServicoSensores(ILeitorSensores? leitor = null, ILoggerFactory? loggerFactory = null)
    {
        var fabrica = loggerFactory ?? NullLoggerFactory.Instance;
        _log = fabrica.CreateLogger<ServicoSensores>();
        _leitor = leitor ?? CriarLeitorPadrao(fabrica);
    }

    public async Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        _log.LogInformation("Lendo sensores (plataforma {Plataforma}).", _leitor.Tipo);

        var leitura = await _leitor.LerAsync(cancellationToken).ConfigureAwait(false);

        _log.LogInformation(
            "Sensores: {Qtd} leitura(s); temperatura máx {Temp}.",
            leitura.Sensores.Count,
            leitura.TemperaturaMaxC?.ToString("0.0", CultureInfo.InvariantCulture) ?? "n/d");

        return leitura;
    }

    private static ILeitorSensores CriarLeitorPadrao(ILoggerFactory fabrica)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new LeitorSensoresLinux(logger: fabrica.CreateLogger<LeitorSensoresLinux>());
        }

        // Produção no Windows: LibreHardwareMonitor (rico) com fallback para WMI
        // (temperatura, sem elevação) quando o driver não está disponível.
        var lhm = new LeitorSensoresLhm(
            new FonteSensoresLhm(fabrica.CreateLogger<FonteSensoresLhm>()),
            fabrica.CreateLogger<LeitorSensoresLhm>());
        var wmi = new LeitorSensoresWindows(fabrica.CreateLogger<LeitorSensoresWindows>());

        return new LeitorSensoresComposto(
            new ILeitorSensores[] { lhm, wmi },
            fabrica.CreateLogger<LeitorSensoresComposto>());
    }
}
