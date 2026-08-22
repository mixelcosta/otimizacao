using System.Diagnostics;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Cleanup;

[SupportedOSPlatform("windows")]
public static class GerenciadorLimpeza
{
    public static IReadOnlyList<CategoriaLimpeza> Escanear(ILogger? logger = null)
    {
        var log = logger ?? NullLogger.Instance;
        return
        [
            EscanearCategoria("Arquivos Temporários do Windows",  "temp_windows",  ObterTemp(),         log),
            EscanearCategoria("Arquivos Temporários do Usuário",  "temp_usuario",  ObterTempUsuario(),  log),
            EscanearCategoria("Cache de Miniaturas",              "thumbnail",     ObterThumbnail(),    log),
            EscanearCategoria("Lixeira",                          "lixeira",       ObterLixeira(),      log),
            EscanearCategoria("Cache do Windows Update",          "update_cache",  ObterUpdateCache(),  log),
            EscanearCategoria("Prefetch",                         "prefetch",      ObterPrefetch(),     log),
            EscanearCategoria("Logs de Erros do Windows",         "event_logs",    ObterEventLogs(),    log),
            EscanearCategoria("Cache de DNS",                     "dns",           [],                  log),  // limpar via ipconfig /flushdns
        ];
    }

    public static ResultadoLimpeza Limpar(IEnumerable<string> ids, ILogger? logger = null)
    {
        var log = logger ?? NullLogger.Instance;
        long totalBytes = 0;
        var erros = new List<string>();

        foreach (var id in ids)
        {
            try
            {
                switch (id)
                {
                    case "temp_windows":  totalBytes += LimparPasta(Path.GetTempPath(), log); break;
                    case "temp_usuario":  totalBytes += LimparPasta(ObterTempUsuario().FirstOrDefault() ?? "", log); break;
                    case "thumbnail":     totalBytes += LimparPasta(ObterThumbnail().FirstOrDefault() ?? "", log); break;
                    case "lixeira":       totalBytes += EsvaziarLixeira(log); break;
                    case "update_cache":  totalBytes += LimparPasta(ObterUpdateCache().FirstOrDefault() ?? "", log); break;
                    case "prefetch":      totalBytes += LimparPasta(ObterPrefetch().FirstOrDefault() ?? "", log); break;
                    case "event_logs":    totalBytes += LimparEventLogs(log); break;
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

    private static long LimparPasta(string pasta, ILogger log)
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
            catch (Exception ex)
            {
                log.LogTrace(ex, "Falha ao apagar arquivo '{Arquivo}' (em uso?)", arq);
            }
        }
        foreach (var dir in Directory.EnumerateDirectories(pasta))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                log.LogTrace(ex, "Falha ao apagar diretório '{Diretorio}' (em uso?)", dir);
            }
        }
        return total;
    }

    private static long EsvaziarLixeira(ILogger log)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName  = "cmd.exe",
                Arguments = "/c rd /s /q %systemdrive%\\$Recycle.Bin 2>nul",
                WindowStyle              = ProcessWindowStyle.Hidden,
                CreateNoWindow           = true,
                UseShellExecute          = false,
            });
            proc?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Falha ao esvaziar a Lixeira");
        }
        return 0;
    }

    private static long LimparEventLogs(ILogger log)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName  = "wevtutil.exe",
                Arguments = "cl System",
                WindowStyle    = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            proc?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Falha ao limpar o Event Log 'System'");
        }
        return 0;
    }

    private static void LimparDns()
    {
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName  = "ipconfig.exe",
            Arguments = "/flushdns",
            WindowStyle    = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        proc?.WaitForExit(3000);
    }

    // ── Scan de tamanho ──────────────────────────────────────────────────────

    private static CategoriaLimpeza EscanearCategoria(string nome, string id, List<string> pastas, ILogger log)
    {
        long bytes = 0;
        foreach (var pasta in pastas)
        {
            if (!Directory.Exists(pasta)) continue;
            try
            {
                int falhas = 0;
                foreach (var f in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
                {
                    try { bytes += new FileInfo(f).Length; }
                    catch { falhas++; }
                }
                if (falhas > 0)
                    log.LogTrace("Falha ao ler tamanho de {Falhas} arquivo(s) em '{Pasta}' (categoria '{Categoria}')", falhas, pasta, id);
            }
            catch (Exception ex)
            {
                log.LogTrace(ex, "Falha ao escanear tamanho da pasta '{Pasta}' (categoria '{Categoria}')", pasta, id);
            }
        }

        return new CategoriaLimpeza(id, nome, bytes);
    }
}
