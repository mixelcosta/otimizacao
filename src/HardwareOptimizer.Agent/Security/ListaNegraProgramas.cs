using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Security;

/// <summary>
/// Define quais programas instalados são seguros para exibir e desinstalar via UI.
/// Runtimes, drivers e componentes do sistema nunca são exibidos ao usuário.
/// </summary>
public static class ListaNegraProgramas
{
    // Padrões no nome que indicam componente do sistema (outras apps dependem destes)
    private static readonly string[] _padroesNomePerigoso =
    [
        // ── Runtimes C++ (dependências silenciosas de outros programas) ───────
        "Microsoft Visual C++",
        "Visual C++ ",
        "VC_redist",
        "Microsoft C Runtime",

        // ── Runtimes .NET (dependências silenciosas de outros programas) ──────
        "Microsoft .NET",
        ".NET Framework",
        ".NET Runtime",
        ".NET Desktop Runtime",
        ".NET Core",
        "ASP.NET Core",
        "Microsoft ASP.NET",

        // ── DirectX (remover quebra jogos e multimídia) ───────────────────────
        "DirectX",
        "Microsoft DirectX",
        "DirectX End-User Runtime",

        // ── SDKs de desenvolvimento ───────────────────────────────────────────
        "Windows SDK",
        "Windows Software Development Kit",
        "Windows Driver Kit",
        "Microsoft Windows SDK",
        "Microsoft Build Tools",
        "Visual Studio Build Tools",
        "Visual Studio Shared",
        "Microsoft Web Deploy",

        // ── Atualizações e patches do Windows ────────────────────────────────
        "Update for Windows",
        "Update for Microsoft",
        "Security Update for",
        "Hotfix for",
        "Cumulative Update",
        "Windows Update",
        "Microsoft Update",

        // ── Drivers e componentes OEM de hardware ────────────────────────────
        "Intel(R) Management Engine",
        "Intel Management Engine",
        "Intel Chipset Device Software",
        "Intel Serial IO",
        "Intel Dynamic Platform",
        "Intel Platform Trust Technology",
        "AMD Chipset",
        "AMD GPIO",
        "AMD Software: Adrenalin",     // driver GPU (não remover)
        "NVIDIA FrameView SDK",
        "NVIDIA HD Audio Driver",
        "Realtek Card Reader",
        "Realtek Ethernet",
        "Realtek Wi-Fi Driver",
        "Qualcomm Atheros",
        "Broadcom Network",

        // ── Componentes XML / MSXML ───────────────────────────────────────────
        "MSXML",
        "Microsoft XML",
        "Microsoft Primary Interoperability",

        // ── Outros componentes críticos do Windows ────────────────────────────
        "Windows App Runtime",
        "Windows Desktop Runtime",
        "Microsoft Edge WebView2",      // runtime usado por muitos apps
        "Microsoft Windows Desktop Runtime",
        "Windows Malicious Software Removal Tool",
    ];

    // Para publisher = Microsoft Corporation: somente estes prefixos são SEGUROS para exibir
    private static readonly string[] _produtosMicrosoftSeguros =
    [
        "Microsoft Edge",              // navegador
        "Microsoft Office",            // suite Office
        "Microsoft 365",               // Office 365
        "Microsoft Teams",
        "Skype",
        "Microsoft Whiteboard",
        "Microsoft To Do",
        "Microsoft Sticky Notes",
        "Microsoft News",
        "Microsoft Solitaire Collection",
        "Microsoft Mahjong",
        "Microsoft Minesweeper",
        "Microsoft PC Manager",
        "Microsoft Power BI Desktop",
        "Microsoft Azure",
        "Microsoft SQL Server Management Studio",
        "Microsoft Visual Studio Code",
        "Microsoft Visual Studio 20",  // IDE (não redistributable)
        "Microsoft Remote Desktop",
        "Microsoft OneDrive",          // cliente de nuvem (seguro remover)
    ];

    /// <summary>
    /// Retorna <c>true</c> se o programa pode ser exibido e desinstalado com segurança.
    /// </summary>
    public static bool EhSeguro(ProgramaInstalado programa)
    {
        var nome       = programa.Nome?.Trim() ?? "";
        var fabricante = programa.Fabricante?.Trim() ?? "";

        // Sem string de desinstalação → não há o que fazer, não exibir
        if (string.IsNullOrWhiteSpace(programa.UninstallString) &&
            string.IsNullOrWhiteSpace(programa.QuietUninstallString))
            return false;

        // Verifica padrões de nome perigosos (independente do fabricante)
        foreach (var padrao in _padroesNomePerigoso)
        {
            if (nome.Contains(padrao, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Para produtos Microsoft: somente exibir os conhecidos como seguros
        if (fabricante.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var seguro in _produtosMicrosoftSeguros)
            {
                if (nome.StartsWith(seguro, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            // Qualquer outro produto Microsoft = componente do sistema → ocultar
            return false;
        }

        return true;
    }
}
