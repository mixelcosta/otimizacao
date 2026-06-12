using System.Management;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Agent.Smart;

/// <summary>
/// Lê dados S.M.A.R.T. dos discos via WMI (somente leitura, sem gravação).
/// Requer Windows; retorna lista vazia em outras plataformas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorSmart : ILeitorSmart
{
    private readonly ILogger<LeitorSmart> _log;

    public LeitorSmart(ILogger<LeitorSmart> log)
    {
        _log = log;
    }

    public IReadOnlyList<DadosSmartBrutos> LerDados()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<DadosSmartBrutos>();

        var resultado = new List<DadosSmartBrutos>();

        try
        {
            // Win32_DiskDrive: modelo e serial
            using var diskSearcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject disk in diskSearcher.Get())
            {
                var modelo = disk["Model"]?.ToString() ?? "Disco desconhecido";
                var serial = disk["SerialNumber"]?.ToString() ?? string.Empty;

                resultado.Add(LerAtributosSmart(modelo, serial));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Não foi possível ler dados S.M.A.R.T. via WMI.");
        }

        return resultado;
    }

    private DadosSmartBrutos LerAtributosSmart(string modelo, string serial)
    {
        long tbwEscritoGb = 0;
        int horasUso = 0;
        bool temErros = false;
        int setoresPendentes = 0;

        try
        {
            // MSStorageDriver_ATAPISmartData contém os atributos SMART brutos
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                $"SELECT * FROM MSStorageDriver_ATAPISmartData WHERE InstanceName LIKE '%{EscaparSerial(serial)}%'");

            foreach (ManagementObject obj in searcher.Get())
            {
                var dados = (byte[]?)obj["VendorSpecific"];
                if (dados is null || dados.Length < 362) continue;

                for (int i = 2; i < dados.Length - 12; i += 12)
                {
                    byte atribId = dados[i];
                    long rawLow = BitConverter.ToInt32(dados, i + 5);
                    long rawHigh = BitConverter.ToInt16(dados, i + 9);
                    long raw = (rawHigh << 32) | (rawLow & 0xFFFFFFFFL);

                    switch (atribId)
                    {
                        case 0xF1: // Total Host Writes
                            tbwEscritoGb = raw / (1024 * 1024 * 2); // sectores → GB
                            break;
                        case 0x09: // Power-On Hours
                            horasUso = (int)(raw & 0xFFFF);
                            break;
                        case 0xBB: // Uncorrectable Error Count
                            temErros = raw > 0;
                            break;
                        case 0xC5: // Current Pending Sector Count
                            setoresPendentes = (int)raw;
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "SMART WMI não disponível para '{Modelo}'.", modelo);
        }

        return new DadosSmartBrutos(modelo, serial, tbwEscritoGb, horasUso, temErros, setoresPendentes);
    }

    private static string EscaparSerial(string serial) =>
        serial.Replace("\\", "\\\\").Replace("'", "\\'").Trim();
}

public sealed record DadosSmartBrutos(
    string Modelo,
    string Serial,
    long TbwEscritoGb,
    int HorasUso,
    bool TemErrosNaoCorrigiveis,
    int SetoresPendentes);
