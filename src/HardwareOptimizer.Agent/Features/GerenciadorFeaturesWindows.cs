using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Features;

/// <summary>
/// Lê o estado e habilita/desabilita Recursos Opcionais do Windows via DISM.
/// A lista curada inclui apenas features seguras e amplamente utilizadas.
/// Leitura usa PowerShell (sem elevação na maioria dos casos); escrita requer
/// elevação (UAC) e é disparada via <c>UseShellExecute=true, Verb="runas"</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public static class GerenciadorFeaturesWindows
{
    /// <summary>Catálogo curado de features seguras para habilitar/desabilitar.</summary>
    internal static readonly IReadOnlyList<InfoFeatureWindows> Catalogo = new InfoFeatureWindows[]
    {
        new() { Nome = "Microsoft-Windows-Subsystem-Linux",
                NomeExibicao = "WSL — Subsistema Linux",
                Descricao    = "Executa distribuições Linux nativamente no Windows." },
        new() { Nome = "Microsoft-Hyper-V-All",
                NomeExibicao = "Hyper-V",
                Descricao    = "Plataforma de virtualização da Microsoft." },
        new() { Nome = "NetFx3",
                NomeExibicao = ".NET Framework 3.5",
                Descricao    = "Suporte a aplicativos legados que requerem .NET 3.5." },
        new() { Nome = "Containers-DisposableClientVM",
                NomeExibicao = "Windows Sandbox",
                Descricao    = "Área isolada e descartável para testar aplicativos com segurança." },
        new() { Nome = "TelnetClient",
                NomeExibicao = "Cliente Telnet",
                Descricao    = "Diagnóstico de rede via protocolo Telnet." },
    };

    /// <summary>Retorna o catálogo curado com o estado atual de cada feature.</summary>
    public static IReadOnlyList<InfoFeatureWindows> ObterInfos() =>
        Catalogo.Select(f => f with { Estado = LerEstado(f.Nome) }).ToList();

    /// <summary>Habilita uma feature via DISM (dispara UAC se necessário).</summary>
    public static async Task<(bool Ok, string? Erro)> HabilitarAsync(string nome, CancellationToken ct) =>
        EhNomeValido(nome)
            ? await ExecutarDismAsync($"/enable-feature /featurename:{nome} /all /norestart", ct).ConfigureAwait(false)
            : (false, $"Feature '{nome}' não está na lista de features suportadas.");

    /// <summary>Desabilita uma feature via DISM (dispara UAC se necessário).</summary>
    public static async Task<(bool Ok, string? Erro)> DesabilitarAsync(string nome, CancellationToken ct) =>
        EhNomeValido(nome)
            ? await ExecutarDismAsync($"/disable-feature /featurename:{nome} /norestart", ct).ConfigureAwait(false)
            : (false, $"Feature '{nome}' não está na lista de features suportadas.");

    private static bool EhNomeValido(string nome) =>
        Catalogo.Any(f => string.Equals(f.Nome, nome, StringComparison.OrdinalIgnoreCase));

    private static string LerEstado(string nome)
    {
        try
        {
            var script  = $"(Get-WindowsOptionalFeature -Online -FeatureName '{nome}').State";
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "Desconhecido";
            var saida = proc.StandardOutput.ReadToEnd().Trim();
            return proc.WaitForExit(10_000) && !string.IsNullOrWhiteSpace(saida) ? saida : "Desconhecido";
        }
        catch
        {
            return "Desconhecido";
        }
    }

    private static async Task<(bool Ok, string? Erro)> ExecutarDismAsync(string args, CancellationToken ct)
    {
        // Executa DISM em sessão elevada via PowerShell + runas.
        var script  = $"dism.exe /online {args}; exit $LASTEXITCODE";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb            = "runas",
            WindowStyle     = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return proc.ExitCode == 0
                ? (true, null)
                : (false, $"DISM saiu com código {proc.ExitCode}.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Operação cancelada (UAC negado).");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
