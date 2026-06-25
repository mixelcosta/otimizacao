using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Cleanup;

[SupportedOSPlatform("windows")]
public static class GerenciadorLimpeza
{
    public static IReadOnlyList<CategoriaLimpeza> Escanear()
    {
        return
        [
            EscanearCategoria("Arquivos Temporários do Windows",  "temp_windows",  ObterTemp()),
            EscanearCategoria("Arquivos Temporários do Usuário",  "temp_usuario",  ObterTempUsuario()),
            EscanearCategoria("Cache de Miniaturas",              "thumbnail",     ObterThumbnail()),
            EscanearCategoria("Lixeira",                          "lixeira",       ObterLixeira()),
            EscanearCategoria("Cache do Windows Update",          "update_cache",  ObterUpdateCache()),
            EscanearCategoria("Prefetch",                         "prefetch",      ObterPrefetch()),
            EscanearCategoria("Logs de Erros do Windows",         "event_logs",    ObterEventLogs()),
            EscanearCategoria("Cache de DNS",                     "dns",           []),  // limpar via ipconfig /flushdns
        ];
    }

    public static ResultadoLimpeza Limpar(IEnumerable<string> ids)
    {
        long totalBytes = 0;
        var erros = new List<string>();

        foreach (var id in ids)
        {
            try
            {
                switch (id)
                {
                    case "temp_windows":  totalBytes += LimparPasta(Path.GetTempPath()); break;
                    case "temp_usuario":  totalBytes += LimparPasta(ObterTempUsuario().FirstOrDefault() ?? ""); break;
                    case "thumbnail":     totalBytes += LimparPasta(ObterThumbnail().FirstOrDefault() ?? ""); break;
                    case "lixeira":       totalBytes += EsvaziarLixeira(); break;
                    case "update_cache":  totalBytes += LimparPasta(ObterUpdateCache().FirstOrDefault() ?? ""); break;
                    case "prefetch":      totalBytes += LimparPasta(ObterPrefetch().FirstOrDefault() ?? ""); break;
                    case "event_logs":    totalBytes += LimparEventLogs(); break;
                    case "dns":           LimparDns(); break;
                }
            }
            catch (Exception ex)
            {
                erros.Add($"[{id}] {ex.Message}");
            }
        }

        return new ResultadoLimpeza(totalBytes, erros);
    }

    // ── Helpers de localização ───────────────────────────────────────────────

    private static List<string> ObterTemp() =>
        [Path.GetTempPath()];

    private static List<string> ObterTempUsuario()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pasta = Path.Combine(user, "Temp");
        return Directory.Exists(pasta) ? [pasta] : [];
    }

    private static List<string> ObterThumbnail()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pasta = Path.Combine(user, @"Microsoft\Windows\Explorer");
        return Directory.Exists(pasta) ? [pasta] : [];
    }

    private static List<string> ObterLixeira() => [];

    private static List<string> ObterUpdateCache()
    {
        var pasta = @"C:\Windows\SoftwareDistribution\Download";
        return Directory.Exists(pasta) ? [pasta] : [];
    }

    private static List<string> ObterPrefetch()
    {
        var pasta = @"C:\Windows\Prefetch";
        return Directory.Exists(pasta) ? [pasta] : [];
    }

    private static List<string> ObterEventLogs() => [];

    // ── Limpeza ──────────────────────────────────────────────────────────────

    private static long LimparPasta(string pasta)
    {
        if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta)) return 0;
        long total = 0;
        foreach (var arq in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
        {
            try
            {
                var fi = new FileInfo(arq);
                total += fi.Length;
                fi.Delete();
            }
            catch { /* arquivo em uso — pular */ }
        }
        foreach (var dir in Directory.EnumerateDirectories(pasta))
        {
            try { Directory.Delete(dir, true); } catch { }
        }
        return total;
    }

    private static long EsvaziarLixeira()
    {
        // Usa SHEmptyRecycleBin via shell
        try
        {
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName  = "cmd.exe",
                Arguments = "/c rd /s /q %systemdrive%\\$Recycle.Bin 2>nul",
                WindowStyle              = System.Diagnostics.ProcessWindowStyle.Hidden,
                CreateNoWindow           = true,
                UseShellExecute          = false,
            });
            proc?.WaitForExit(5000);
        }
        catch { }
        return 0;
    }

    private static long LimparEventLogs()
    {
        try
        {
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName  = "wevtutil.exe",
                Arguments = "cl System",
                WindowStyle    = System.Diagnostics.ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            proc?.WaitForExit(5000);
        }
        catch { }
        return 0;
    }

    private static void LimparDns()
    {
        var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName  = "ipconfig.exe",
            Arguments = "/flushdns",
            WindowStyle    = System.Diagnostics.ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        proc?.WaitForExit(3000);
    }

    // ── Scan de tamanho ──────────────────────────────────────────────────────

    private static CategoriaLimpeza EscanearCategoria(string nome, string id, List<string> pastas)
    {
        long bytes = 0;
        foreach (var pasta in pastas)
        {
            if (!Directory.Exists(pasta)) continue;
            try
            {
                foreach (var f in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
                {
                    try { bytes += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
        }

        // Lixeira e DNS não têm pasta — reportar estimativa
        if (id == "lixeira")
        {
            try
            {
                var lixeiraDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
                // GetFolderSize via shell não disponível facilmente — reportar 0 (será limpo mesmo assim)
            }
            catch { }
        }

        return new CategoriaLimpeza(id, nome, bytes);
    }
}
