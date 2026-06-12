using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores via WMI (sem necessidade de elevação). Fornece temperatura
/// de zona térmica da CPU, clock atual do processador e throughput de disco.
/// Complementa o LibreHardwareMonitor no <see cref="LeitorSensoresComposto"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorSensoresWindows : ILeitorSensores
{
    private readonly ILogger _log;
    // WMI spawna PowerShell (~3-5s): semáforo garante uma única execução por vez;
    // cache de 8s evita chamadas desnecessárias no timer de 500ms.
    private readonly SemaphoreSlim _wmiLock = new(1, 1);
    private LeituraSensores? _cache;
    private DateTime _cacheExpira = DateTime.MinValue;
    private static readonly TimeSpan _intervaloWmi = TimeSpan.FromSeconds(8);

    private const string ScriptWmi = @"
$out = @{}
try {
    $tz = Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue
    if ($tz) { $out.thermalZones = @($tz | Select-Object CurrentTemperature) }
} catch {}
try {
    $cpu = Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue
    if ($cpu) { $out.cpuClock = @($cpu | Select-Object CurrentClockSpeed, LoadPercentage) }
} catch {}
try {
    $disk = Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk -Filter ""Name='_Total'"" -ErrorAction SilentlyContinue
    if ($disk) { $out.disk = @($disk | Select-Object DiskReadBytesPersec,DiskWriteBytesPersec) }
} catch {}
try {
    $gpuSamples = (Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue).CounterSamples
    if ($gpuSamples) {
        $total = ($gpuSamples | Measure-Object CookedValue -Sum).Sum
        $out.gpuUtil = [math]::Round($total, 1)
    }
} catch {}
$out | ConvertTo-Json -Depth 3 -Compress";

    public LeitorSensoresWindows(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache is not null && DateTime.UtcNow < _cacheExpira)
            return Task.FromResult(_cache);

        // Não bloqueia: se WMI já está rodando em outra thread, devolve o cache atual
        // (pode ser null na primeira vez — nesse caso LHM cobre os outros sensores).
        if (!_wmiLock.Wait(0))
            return Task.FromResult(_cache ?? new LeituraSensores());

        // Re-verifica depois de adquirir o lock
        if (_cache is not null && DateTime.UtcNow < _cacheExpira)
        {
            _wmiLock.Release();
            return Task.FromResult(_cache);
        }

        try
        {
            var sensores = ExecutarConsultasWmi();
            _log.LogDebug("Leitor WMI: {Qtd} sensor(es) lidos.", sensores.Count);
            _cache = new LeituraSensores { Sensores = sensores };
            _cacheExpira = DateTime.UtcNow + _intervaloWmi;
            return Task.FromResult(_cache);
        }
        finally
        {
            _wmiLock.Release();
        }
    }

    private List<Sensor> ExecutarConsultasWmi()
    {
        var sensores = new List<Sensor>();
        var json = ExecutarPowerShell(ScriptWmi);
        if (string.IsNullOrWhiteSpace(json))
        {
            _log.LogWarning("Leitor WMI: resposta vazia do PowerShell.");
            return sensores;
        }

        JsonDocument? doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { _log.LogWarning(ex, "Leitor WMI: JSON inválido."); return sensores; }

        using (doc)
        {
            var root = doc.RootElement;

            // Temperatura da CPU via zona térmica ACPI
            if (root.TryGetProperty("thermalZones", out var zones) && zones.ValueKind == JsonValueKind.Array)
            {
                int idx = 1;
                foreach (var zone in zones.EnumerateArray())
                {
                    if (zone.TryGetProperty("CurrentTemperature", out var ct) && ct.TryGetDouble(out var raw))
                    {
                        var celsius = Math.Round(raw / 10.0 - 273.15, 1);
                        if (celsius > 0 && celsius < 150)
                        {
                            sensores.Add(new Sensor
                            {
                                Nome = $"[CPU] Thermal Zone {idx} / Temperature",
                                Tipo = TipoSensor.Temperatura,
                                Valor = celsius,
                                Unidade = "°C",
                            });
                        }
                    }
                    idx++;
                }
            }

            // Clock atual e carga do processador
            if (root.TryGetProperty("cpuClock", out var clocks) && clocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var cpu in clocks.EnumerateArray())
                {
                    if (cpu.TryGetProperty("CurrentClockSpeed", out var cs) && cs.TryGetDouble(out var mhz) && mhz > 0)
                        sensores.Add(new Sensor { Nome = "[CPU] Processor / Clock", Tipo = TipoSensor.Clock, Valor = mhz, Unidade = "MHz" });

                    if (cpu.TryGetProperty("LoadPercentage", out var lp) && lp.TryGetDouble(out var load))
                        sensores.Add(new Sensor { Nome = "[CPU] Processor / Load", Tipo = TipoSensor.Carga, Valor = load, Unidade = "%" });

                    break;
                }
            }

            // Throughput de disco (agregado _Total)
            if (root.TryGetProperty("disk", out var disks) && disks.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in disks.EnumerateArray())
                {
                    if (d.TryGetProperty("DiskReadBytesPersec", out var rb) && rb.TryGetDouble(out var rBytes))
                        sensores.Add(new Sensor { Nome = "[STORAGE] _Total / Read Rate", Tipo = TipoSensor.Outro, Valor = Math.Round(rBytes / 1_048_576.0, 2), Unidade = "MB/s" });

                    if (d.TryGetProperty("DiskWriteBytesPersec", out var wb) && wb.TryGetDouble(out var wBytes))
                        sensores.Add(new Sensor { Nome = "[STORAGE] _Total / Write Rate", Tipo = TipoSensor.Outro, Valor = Math.Round(wBytes / 1_048_576.0, 2), Unidade = "MB/s" });

                    break;
                }
            }

            // Utilização GPU via Windows Performance Counters (fallback quando LHM/ADL não disponível)
            if (root.TryGetProperty("gpuUtil", out var gpuUtilEl) && gpuUtilEl.TryGetDouble(out var gpuUtil) && gpuUtil >= 0)
                sensores.Add(new Sensor { Nome = "[GPU] Adapter / Load", Tipo = TipoSensor.Carga, Valor = Math.Round(gpuUtil, 1), Unidade = "%" });
        }
        return sensores;
    }

    private string? ExecutarPowerShell(string script)
    {
        try
        {
            // Usa -EncodedCommand (UTF-16LE base64) para evitar problemas de escaping
            // em scripts com aspas internas.
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;
            var saida = proc.StandardOutput.ReadToEnd();
            return proc.WaitForExit(25_000) ? saida.Trim() : null;
        }
        catch (Win32Exception) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (IOException) { return null; }
    }
}
