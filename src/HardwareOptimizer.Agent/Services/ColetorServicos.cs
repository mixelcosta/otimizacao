using System.Management;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Agent.Services;

/// <summary>
/// Enumera todos os serviços Windows via WMI (Win32_Service).
/// Usa Task.Run para não bloquear a thread de chamada.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ColetorServicos
{
    private readonly ILogger<ColetorServicos> _log;

    public ColetorServicos(ILogger<ColetorServicos> log) => _log = log;

    public Task<IReadOnlyList<ServicoWindows>> ColetarAsync(CancellationToken ct = default) =>
        Task.Run(() => Coletar(), ct);

    private IReadOnlyList<ServicoWindows> Coletar()
    {
        var lista = new List<ServicoWindows>();

        try
        {
            const string query = "SELECT Name, DisplayName, ProcessId, State, StartName, StartMode FROM Win32_Service";
            using var searcher = new ManagementObjectSearcher(query);
            using var results  = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                var nome = obj["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(nome)) continue;

                lista.Add(new ServicoWindows
                {
                    Nome      = nome,
                    Descricao = obj["DisplayName"]?.ToString() ?? nome,
                    Pid       = Convert.ToInt32(obj["ProcessId"] ?? 0),
                    Status    = obj["State"]?.ToString() ?? "Unknown",
                    Grupo     = SimplificarGrupo(obj["StartName"]?.ToString()),
                    ModoInicio = obj["StartMode"]?.ToString(),
                });
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao enumerar serviços via WMI.");
        }

        return lista.OrderBy(s => s.Nome, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? SimplificarGrupo(string? startName) => startName switch
    {
        "LocalSystem"                  => "LocalSystem",
        "NT AUTHORITY\\NetworkService" => "NetworkService",
        "NT AUTHORITY\\LocalService"   => "LocalService",
        null or "" => null,
        _ => startName,
    };
}
