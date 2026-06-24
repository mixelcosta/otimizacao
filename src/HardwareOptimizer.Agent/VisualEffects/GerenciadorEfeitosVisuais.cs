using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Win32;

namespace HardwareOptimizer.Agent.VisualEffects;

/// <summary>
/// Lê e aplica individualmente cada efeito visual do Windows,
/// espelhando os itens da tela "Opções de Desempenho → Efeitos Visuais".
/// </summary>
[SupportedOSPlatform("windows")]
public static class GerenciadorEfeitosVisuais
{
    // ── SPI codes ────────────────────────────────────────────────────────────
    private const uint SPI_GETANIMATION            = 0x0048;
    private const uint SPI_SETANIMATION            = 0x0049;
    private const uint SPI_GETDRAGFULLWINDOWS      = 0x0026;
    private const uint SPI_SETDRAGFULLWINDOWS      = 0x0025;
    private const uint SPI_GETFONTSMOOTHING        = 0x004A;
    private const uint SPI_SETFONTSMOOTHING        = 0x004B;
    private const uint SPI_GETCOMBOBOXANIMATION    = 0x1004;
    private const uint SPI_SETCOMBOBOXANIMATION    = 0x1005;
    private const uint SPI_GETLISTBOXSMOOTHSCROLL  = 0x100C;
    private const uint SPI_SETLISTBOXSMOOTHSCROLL  = 0x100D;
    private const uint SPI_GETGRADIENTCAPTIONS     = 0x100E;
    private const uint SPI_SETGRADIENTCAPTIONS     = 0x100F;
    private const uint SPI_GETUIEFFECTS            = 0x103E;
    private const uint SPI_SETUIEFFECTS            = 0x103F;
    private const uint SPI_GETMENUANIMATION        = 0x1002;
    private const uint SPI_SETMENUANIMATION        = 0x1003;
    private const uint SPI_GETMENUFADE             = 0x1012;
    private const uint SPI_SETMENUFADE             = 0x1013;
    private const uint SPI_GETSELECTIONFADE        = 0x1014;
    private const uint SPI_SETSELECTIONFADE        = 0x1015;
    private const uint SPI_GETTOOLTIPANIMATION     = 0x1016;
    private const uint SPI_SETTOOLTIPANIMATION     = 0x1017;
    private const uint SPI_GETCURSORSHADOW         = 0x101A;
    private const uint SPI_SETCURSORSHADOW         = 0x101B;
    private const uint SPI_GETDROPSHADOW           = 0x1024;
    private const uint SPI_SETDROPSHADOW           = 0x1025;

    private const uint SPIF_UPDATEINIFILE = 0x0001;
    private const uint SPIF_SENDCHANGE    = 0x0002;
    private const uint SPIF_APPLY        = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;

    // ── Registry paths ───────────────────────────────────────────────────────
    private const string KeyExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string KeyDwm             = @"Software\Microsoft\Windows\DWM";

    // ── ANIMATIONINFO struct ─────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct ANIMATIONINFO
    {
        public uint cbSize;
        public int  iMinAnimate;
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref bool result, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref ANIMATIONINFO result, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, IntPtr result, uint winIni);

    // ── Leitura ──────────────────────────────────────────────────────────────

    public static IReadOnlyList<EfeitoVisual> ObterTodos()
    {
        var r = new List<EfeitoVisual>
        {
            Ef("ComboBoxAnimation",   "Abrir caixas de combinação",                              LerSpi(SPI_GETCOMBOBOXANIMATION)),
            Ef("TaskbarAnimations",   "Animações na barra de tarefas",                           LerRegistroBool(KeyExplorerAdvanced, "TaskbarAnimations", defaultTrue: true)),
            Ef("UiEffects",           "Animar controles e elementos no Windows",                 LerSpi(SPI_GETUIEFFECTS)),
            Ef("WindowAnimation",     "Animar janelas ao minimizar e maximizar",                 LerAnimacaoJanela()),
            Ef("SelectionFade",       "Esmaecer itens de menu após clicados",                   LerSpi(SPI_GETSELECTIONFADE)),
            Ef("TooltipAnimation",    "Esmaecer ou deslizar Dicas de ferramenta para a exibição",LerSpi(SPI_GETTOOLTIPANIMATION)),
            Ef("MenuAnimation",       "Esmaecer ou deslizar menus para a exibição",              LerSpiMenuAnimacao()),
            Ef("AeroPeek",            "Habilitar o Peek",                                       LerRegistroBool(KeyDwm, "EnableAeroPeek", defaultTrue: true)),
            Ef("DragFullWindows",     "Mostrar conteúdo da janela ao arrastar",                  LerSpiDragFull()),
            Ef("Thumbnails",          "Mostrar miniaturas em vez de ícones",                     !LerRegistroBool(KeyExplorerAdvanced, "IconsOnly", defaultTrue: false)),
            Ef("ListviewAlphaSelect", "Mostrar retângulo de seleção translúcido",                LerRegistroBool(KeyExplorerAdvanced, "ListviewAlphaSelect", defaultTrue: true)),
            Ef("DropShadow",          "Mostrar sombras sob janelas",                             LerSpi(SPI_GETDROPSHADOW)),
            Ef("CursorShadow",        "Mostrar sombras sob o ponteiro do mouse",                LerSpi(SPI_GETCURSORSHADOW)),
            Ef("ListboxSmoothScroll", "Rolar caixas de listagem suavemente",                    LerSpi(SPI_GETLISTBOXSMOOTHSCROLL)),
            Ef("ThumbnailCache",      "Salvar visualizações de miniaturas da barra de tarefas", !LerRegistroBool(KeyExplorerAdvanced, "DisableThumbnailCache", defaultTrue: false)),
            Ef("FontSmoothing",       "Usar fontes de tela com cantos arredondados",             LerSpiDragFull2(SPI_GETFONTSMOOTHING)),
            Ef("ListviewShadow",      "Usar sombras para rótulos de ícones na área de trabalho",LerRegistroBool(KeyExplorerAdvanced, "ListviewShadow", defaultTrue: true)),
        };
        return r;
    }

    // ── Escrita ──────────────────────────────────────────────────────────────

    public static bool Aplicar(string id, bool ativo)
    {
        return id switch
        {
            "ComboBoxAnimation"   => EscreverSpi(SPI_SETCOMBOBOXANIMATION, ativo),
            "TaskbarAnimations"   => EscreverRegistro(KeyExplorerAdvanced, "TaskbarAnimations", ativo ? 1 : 0),
            "UiEffects"           => EscreverSpi(SPI_SETUIEFFECTS, ativo),
            "WindowAnimation"     => EscreverAnimacaoJanela(ativo),
            "SelectionFade"       => EscreverSpi(SPI_SETSELECTIONFADE, ativo),
            "TooltipAnimation"    => EscreverSpi(SPI_SETTOOLTIPANIMATION, ativo),
            "MenuAnimation"       => EscreverSpiMenu(ativo),
            "AeroPeek"            => EscreverRegistro(KeyDwm, "EnableAeroPeek", ativo ? 1 : 0),
            "DragFullWindows"     => EscreverSpiInt(SPI_SETDRAGFULLWINDOWS, ativo ? 1 : 0),
            "Thumbnails"          => EscreverRegistro(KeyExplorerAdvanced, "IconsOnly", ativo ? 0 : 1),
            "ListviewAlphaSelect" => EscreverRegistro(KeyExplorerAdvanced, "ListviewAlphaSelect", ativo ? 1 : 0),
            "DropShadow"          => EscreverSpi(SPI_SETDROPSHADOW, ativo),
            "CursorShadow"        => EscreverSpi(SPI_SETCURSORSHADOW, ativo),
            "ListboxSmoothScroll" => EscreverSpi(SPI_SETLISTBOXSMOOTHSCROLL, ativo),
            "ThumbnailCache"      => EscreverRegistro(KeyExplorerAdvanced, "DisableThumbnailCache", ativo ? 0 : 1),
            "FontSmoothing"       => EscreverSpiInt(SPI_SETFONTSMOOTHING, ativo ? 1 : 0),
            "ListviewShadow"      => EscreverRegistro(KeyExplorerAdvanced, "ListviewShadow", ativo ? 1 : 0),
            _ => false,
        };
    }

    // ── Helpers de leitura ───────────────────────────────────────────────────

    private static bool LerSpi(uint code)
    {
        bool v = false;
        SystemParametersInfo(code, 0, ref v, 0);
        return v;
    }

    private static bool LerSpiDragFull()
    {
        // SPI_GETDRAGFULLWINDOWS usa ref bool
        bool v = false;
        SystemParametersInfo(SPI_GETDRAGFULLWINDOWS, 0, ref v, 0);
        return v;
    }

    private static bool LerSpiDragFull2(uint code)
    {
        bool v = false;
        SystemParametersInfo(code, 0, ref v, 0);
        return v;
    }

    private static bool LerAnimacaoJanela()
    {
        var info = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>() };
        SystemParametersInfo(SPI_GETANIMATION, info.cbSize, ref info, 0);
        return info.iMinAnimate != 0;
    }

    private static bool LerSpiMenuAnimacao()
    {
        // Menu animation == true if either menuanimation or menufade is on
        bool anim = false, fade = false;
        SystemParametersInfo(SPI_GETMENUANIMATION, 0, ref anim, 0);
        SystemParametersInfo(SPI_GETMENUFADE, 0, ref fade, 0);
        return anim || fade;
    }

    private static bool LerRegistroBool(string keyPath, string valueName, bool defaultTrue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            if (key is null) return defaultTrue;
            var raw = key.GetValue(valueName);
            if (raw is int i) return i != 0;
            return defaultTrue;
        }
        catch { return defaultTrue; }
    }

    // ── Helpers de escrita ───────────────────────────────────────────────────

    private static bool EscreverSpi(uint code, bool ativo)
    {
        var val = ativo ? 1u : 0u;
        return SystemParametersInfo(code, val, IntPtr.Zero, SPIF_APPLY);
    }

    private static bool EscreverSpiInt(uint code, int val)
    {
        return SystemParametersInfo(code, (uint)val, IntPtr.Zero, SPIF_APPLY);
    }

    private static bool EscreverAnimacaoJanela(bool ativo)
    {
        var info = new ANIMATIONINFO
        {
            cbSize     = (uint)Marshal.SizeOf<ANIMATIONINFO>(),
            iMinAnimate = ativo ? 1 : 0,
        };
        return SystemParametersInfo(SPI_SETANIMATION, info.cbSize, ref info, SPIF_APPLY);
    }

    private static bool EscreverSpiMenu(bool ativo)
    {
        var ok1 = SystemParametersInfo(SPI_SETMENUANIMATION, ativo ? 1u : 0u, IntPtr.Zero, SPIF_APPLY);
        var ok2 = SystemParametersInfo(SPI_SETMENUFADE, ativo ? 1u : 0u, IntPtr.Zero, SPIF_APPLY);
        return ok1 && ok2;
    }

    private static bool EscreverRegistro(string keyPath, string valueName, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
            key?.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }

    private static EfeitoVisual Ef(string id, string nome, bool ativo) => new(id, nome, ativo);
}
