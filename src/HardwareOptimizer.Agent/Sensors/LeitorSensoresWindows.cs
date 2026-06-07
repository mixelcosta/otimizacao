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
/// Leitor de sensores para Windows. Lê a temperatura via WMI
/// (MSAcpi_ThermalZoneTemperature) por PowerShell, em modo somente leitura. Para
/// dados ricos (clock, voltagem, fan, consumo por componente), a implementação
/// de produção usa LibreHardwareMonitorLib (driver de kernel assinado; atentar a
/// Secure Boot). Defensivo: falhas resultam em leitura vazia, nunca em exceção.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorSensoresWindows : ILeitorSensores
{
    private readonly ILogger _log;

    public LeitorSensoresWindows(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo temperatura via WMI (MSAcpi_ThermalZoneTemperature).");

        var sensores = new List<Sensor>();
        var indice = 1;
        foreach (var decimosKelvin in LerTemperaturas())
        {
            var celsius = Math.Round((decimosKelvin / 10.0) - 273.15, 1);
            sensores.Add(new Sensor
            {
                Nome = $"Zona térmica {indice++}",
                Tipo = TipoSensor.Temperatura,
                Valor = celsius,
                Unidade = "°C",
            });
        }

        if (sensores.Count == 0)
        {
            _log.LogWarning("Nenhuma zona térmica WMI legível (use LibreHardwareMonitor para dados completos).");
        }

        return Task.FromResult(new LeituraSensores { Sensores = sensores });
    }

    private IEnumerable<double> LerTemperaturas()
    {
        var saida = ExecutarPowerShell(
            "Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature "
            + "| Select-Object CurrentTemperature | ConvertTo-Json -Compress");
        if (string.IsNullOrWhiteSpace(saida))
        {
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(saida);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (ExtrairTemperatura(item) is { } valor)
                    {
                        yield return valor;
                    }
                }
            }
            else if (ExtrairTemperatura(doc.RootElement) is { } unica)
            {
                yield return unica;
            }
        }
    }

    private static double? ExtrairTemperatura(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty("CurrentTemperature", out var prop)
        && prop.ValueKind == JsonValueKind.Number
        && prop.TryGetDouble(out var valor)
            ? valor
            : null;

    private string? ExecutarPowerShell(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{comando}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(psi);
            if (processo is null)
            {
                return null;
            }

            var saida = processo.StandardOutput.ReadToEnd();
            return processo.WaitForExit(20_000) ? saida.Trim() : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
