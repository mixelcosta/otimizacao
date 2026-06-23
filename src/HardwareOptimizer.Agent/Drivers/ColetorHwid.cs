using System.Management;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Agent.Drivers;

/// <summary>
/// Coleta HardwareID de dispositivos via WMI (Win32_PnPEntity).
/// Filtra apenas as categorias relevantes para drivers do usuário final.
/// Operação somente leitura.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ColetorHwid : IColetorHwid
{
    private static readonly HashSet<string> _categoriasRelevantes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display",          // GPU
        "Net",              // Adaptadores de rede
        "AudioEndpoint",    // Áudio
        "Media",            // Controladores de mídia/codecs
        "USB",              // Controladores USB
        "DiskDrive",        // Controladores de armazenamento
        "HDC",              // IDE/SATA controllers
    };

    private readonly ILogger<ColetorHwid> _log;

    public ColetorHwid(ILogger<ColetorHwid> log)
    {
        _log = log;
    }

    public IReadOnlyList<InfoDriver> Coletar()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<InfoDriver>();

        var resultado = new List<InfoDriver>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Name, Manufacturer, DriverVersion, ClassGuid, DeviceClass FROM Win32_PnPSignedDriver");

            foreach (ManagementObject obj in searcher.Get())
            {
                var deviceClass = obj["DeviceClass"]?.ToString() ?? string.Empty;
                if (!_categoriasRelevantes.Any(c =>
                    deviceClass.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var hwid = obj["DeviceID"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(hwid)) continue;

                resultado.Add(new InfoDriver
                {
                    HardwareId = hwid,
                    Descricao = obj["Name"]?.ToString() ?? hwid,
                    Fabricante = obj["Manufacturer"]?.ToString(),
                    VersaoAtual = obj["DriverVersion"]?.ToString(),
                    Status = StatusDriver.Desconhecido,
                });
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao coletar HWIDs via WMI.");
        }

        _log.LogInformation("HWID: {Qtd} dispositivos relevantes encontrados.", resultado.Count);
        return resultado;
    }
}
