using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HardwareOptimizer.App.Services;

/// <summary>
/// Exibe balloon tips na taskbar do Windows via Shell_NotifyIcon (Win32).
/// Deve ser criado após a janela principal estar visível (precisa do HWND).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServicoNotificacaoWindows : IDisposable
{
    // ── Win32 constants ──────────────────────────────────────────────────────

    private const uint NIM_ADD     = 0x00000000;
    private const uint NIM_MODIFY  = 0x00000001;
    private const uint NIM_DELETE  = 0x00000002;
    private const uint NIF_ICON    = 0x00000002;
    private const uint NIF_TIP     = 0x00000004;
    private const uint NIF_INFO    = 0x00000010;
    private const uint NIIF_WARNING = 0x00000002;
    private const nint IDI_APPLICATION = 32512;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint  cbSize;
        public nint  hWnd;
        public uint  uID;
        public uint  uFlags;
        public uint  uCallbackMessage;
        public nint  hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint  dwState;
        public uint  dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint  uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint  dwInfoFlags;
        public Guid  guidItem;
        public nint  hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint LoadIcon(nint hInstance, nint lpIconName);

    // ────────────────────────────────────────────────────────────────────────

    private readonly nint _hwnd;
    private readonly nint _hIcon;
    private bool _adicionado;
    private bool _descartado;

    private static uint _idSequencial = 1000;
    private readonly uint _id;

    public ServicoNotificacaoWindows(nint hwnd)
    {
        _hwnd  = hwnd;
        _hIcon = LoadIcon(nint.Zero, IDI_APPLICATION);
        _id    = System.Threading.Interlocked.Increment(ref _idSequencial);
        Adicionar();
    }

    private void Adicionar()
    {
        var data = Criar(NIF_ICON | NIF_TIP);
        data.hIcon  = _hIcon;
        data.szTip  = "Otimize Builder";
        _adicionado = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    public void MostrarAlerta(string titulo, string mensagem)
    {
        if (!_adicionado || _descartado) return;

        var data = Criar(NIF_INFO);
        data.szInfo       = mensagem.Length > 255 ? mensagem[..255] : mensagem;
        data.szInfoTitle  = titulo.Length > 63 ? titulo[..63] : titulo;
        data.dwInfoFlags  = NIIF_WARNING;
        data.uTimeoutOrVersion = 5000;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true;

        if (_adicionado)
        {
            var data = Criar(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
        }
    }

    private NOTIFYICONDATA Criar(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd   = _hwnd,
        uID    = _id,
        uFlags = flags,
        szTip  = "Otimize Builder",
        szInfo = "",
        szInfoTitle = "",
    };
}
