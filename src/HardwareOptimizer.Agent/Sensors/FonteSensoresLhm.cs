using System.ComponentModel;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Implementação real de <see cref="IFonteSensoresLhm"/> sobre
/// LibreHardwareMonitorLib. Abre o <see cref="Computer"/> uma vez (carrega o
/// driver de kernel assinado — atentar a Secure Boot/elevação) e, a cada leitura,
/// atualiza o hardware e projeta os sensores suportados no contrato do domínio.
/// Defensiva: falhas de driver/permissão viram leitura parcial/vazia, nunca
/// exceção que derrube o serviço de sensores.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FonteSensoresLhm : IFonteSensoresLhm, IDisposable
{
    private readonly Computer _computer;
    private readonly ILogger _log;
    private bool _aberto;
    private int _tentativasGpuSemDados;
    private const int MaxTentativasGpu = 2;

    public FonteSensoresLhm(ILogger? logger = null)
    {
        _log = logger ?? NullLogger.Instance;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = false,
        };
    }

    public IReadOnlyList<Sensor> Ler()
    {
        var sensores = new List<Sensor>();
        try
        {
            if (!_aberto)
            {
                _computer.Open();
                _aberto = true;
            }

            // Tenta redescobrir GPU no máximo MaxTentativasGpu vezes (não a cada tick).
            // Re-init a cada 500ms corromperia o estado interno do LHM.
            var temGpu = _computer.Hardware.Any(h =>
                h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel);

            if (!temGpu && _tentativasGpuSemDados < MaxTentativasGpu)
            {
                _tentativasGpuSemDados++;
                _log.LogDebug("GPU não detectada pelo LHM (tentativa {N}/{Max}) — reinicializando.", _tentativasGpuSemDados, MaxTentativasGpu);
                try { _computer.Close(); } catch { /* ignora */ }
                _computer.Open();
            }

            foreach (var hardware in _computer.Hardware)
            {
                ColetarHardware(hardware, sensores);
            }
        }
        catch (Exception ex) when (
            ex is Win32Exception or DllNotFoundException or UnauthorizedAccessException
               or InvalidOperationException or IOException or BadImageFormatException)
        {
            _log.LogWarning(ex, "Falha ao ler sensores via LibreHardwareMonitor (driver/elevação?).");
        }

        return sensores;
    }

    private static void ColetarHardware(IHardware hardware, List<Sensor> destino)
    {
        hardware.Update();

        foreach (var sub in hardware.SubHardware)
        {
            ColetarHardware(sub, destino);
        }

        var prefixo = hardware.HardwareType switch
        {
            HardwareType.Cpu => "[CPU]",
            HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel => "[GPU]",
            HardwareType.Memory => "[RAM]",
            HardwareType.Storage => "[STORAGE]",
            HardwareType.Motherboard or HardwareType.SuperIO => "[MB]",
            _ => "[HW]",
        };

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not { } valor || MapearTipo(sensor.SensorType) is not { } tipo)
            {
                continue;
            }

            destino.Add(new Sensor
            {
                Nome = $"{prefixo} {hardware.Name} / {sensor.Name}",
                Tipo = tipo,
                Valor = Math.Round((double)valor, 2),
                Unidade = UnidadeDe(tipo),
            });
        }
    }

    private static TipoSensor? MapearTipo(SensorType tipo) => tipo switch
    {
        SensorType.Temperature => TipoSensor.Temperatura,
        SensorType.Clock => TipoSensor.Clock,
        SensorType.Voltage => TipoSensor.Voltagem,
        SensorType.Fan => TipoSensor.Fan,
        SensorType.Power => TipoSensor.Potencia,
        SensorType.Load => TipoSensor.Carga,
        SensorType.Data => TipoSensor.Outro,
        SensorType.Throughput => TipoSensor.Outro,
        _ => null,
    };

    private static string UnidadeDe(TipoSensor tipo) => tipo switch
    {
        TipoSensor.Temperatura => "°C",
        TipoSensor.Clock => "MHz",
        TipoSensor.Voltagem => "V",
        TipoSensor.Fan => "RPM",
        TipoSensor.Potencia => "W",
        _ => string.Empty,
    };

    public void Dispose()
    {
        if (!_aberto)
        {
            return;
        }

        try
        {
            _computer.Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _log.LogDebug(ex, "Falha ao fechar o LibreHardwareMonitor.");
        }
        finally
        {
            _aberto = false;
        }
    }
}
