# driver.ps1 — Otimize Builder interaction driver
# Usage: .\driver.ps1 [-Action <screenshot|scan|navigate|scroll>] [-Target <pagename>] [-Out <path>]
# All paths are relative to repo root (caller's CWD).
# Requires: .NET 8 SDK, Windows (Win32 APIs used directly).

param(
    [string]$Action  = "screenshot",
    [string]$Target  = "",
    [string]$Out     = "C:\Temp\otimize_shot.png",
    [int]   $Delay   = 500
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

public class OtimizeDriver {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lp, IntPtr param);
    [DllImport("user32.dll")] public static extern int  GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int  GetDpiForWindow(IntPtr h);

    public delegate bool EnumWindowsProc(IntPtr h, IntPtr p);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int L, T, R, B; }

    public static IntPtr FindByTitle(string partial) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            if (!IsWindowVisible(h)) return true;
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(h, sb, 256);
            if (sb.ToString().Contains(partial)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // nFlags=2: captures Avalonia GPU-composited content correctly (nFlags=0 returns blank)
    public static void Screenshot(IntPtr hwnd, string path) {
        RECT r;
        GetClientRect(hwnd, out r);
        int w = r.R - r.L, h = r.B - r.T;
        Bitmap bmp = new Bitmap(w, h);
        Graphics g = Graphics.FromImage(bmp);
        IntPtr hdc = g.GetHdc();
        PrintWindow(hwnd, hdc, 2);
        g.ReleaseHdc(hdc);
        g.Dispose();
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }

    // Client-area coordinates (not window-relative — title bar excluded)
    public static void Click(IntPtr hwnd, int x, int y) {
        IntPtr lp = (IntPtr)((y << 16) | (x & 0xFFFF));
        PostMessage(hwnd, 0x0201, (IntPtr)1, lp);
        System.Threading.Thread.Sleep(60);
        PostMessage(hwnd, 0x0202, (IntPtr)0, lp);
    }

    // WM_MOUSEWHEEL: lParam must be screen-absolute coords
    public static void Scroll(IntPtr hwnd, int screenX, int screenY, int delta) {
        IntPtr lp = (IntPtr)((screenY << 16) | (screenX & 0xFFFF));
        IntPtr wp = (IntPtr)(delta << 16);
        PostMessage(hwnd, 0x020A, wp, lp);
    }
}
'@ -ReferencedAssemblies System.Drawing

$hwnd = [OtimizeDriver]::FindByTitle("Otimize")
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Error "Otimize Builder window not found. Launch with: dotnet run --project src/HardwareOptimizer.App --configuration Release"
    exit 1
}

# Title bar height = WinRect height - ClientRect height (confirmed 39px at DPI=96)
$wr = New-Object OtimizeDriver+RECT
[OtimizeDriver]::GetWindowRect($hwnd, [ref]$wr) | Out-Null
$cr = New-Object OtimizeDriver+RECT
[OtimizeDriver]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$titleBarH = ($wr.B - $wr.T) - $cr.B

# Sidebar client-Y coords (measured at DPI=96/100% scaling; title bar already excluded)
$sidebarX = 97
$sidebar = @{
    "Dashboard"         = 143
    "OtimizadorWindows" = 186
    "InfoSistema"       = 189
    "IaCopiloto"        = 231
    "Configuracoes"     = 671
}

switch ($Action.ToLower()) {

    "screenshot" {
        [OtimizeDriver]::Screenshot($hwnd, $Out)
        $cw = $cr.R; $ch = $cr.B
        Write-Host "screenshot -> $Out  (${cw}x${ch})"
    }

    "navigate" {
        if (-not $sidebar.ContainsKey($Target)) {
            Write-Error "Unknown target '$Target'. Valid: $($sidebar.Keys -join ', ')"
            exit 1
        }
        [OtimizeDriver]::Click($hwnd, $sidebarX, $sidebar[$Target])
        Start-Sleep -Milliseconds $Delay
        [OtimizeDriver]::Screenshot($hwnd, $Out)
        Write-Host "navigated to $Target -> $Out"
    }

    "scan" {
        # SCAN circle center in client coordinates
        [OtimizeDriver]::Click($hwnd, 652, 361)
        Write-Host "SCAN triggered, waiting ${Delay}ms..."
        Start-Sleep -Milliseconds $Delay
        [OtimizeDriver]::Screenshot($hwnd, $Out)
        Write-Host "scan -> $Out"
    }

    "scroll" {
        # Convert client-area center to screen coords for WM_MOUSEWHEEL
        $screenX = $wr.L + 600
        $screenY = $wr.T + $titleBarH + 400
        [OtimizeDriver]::Scroll($hwnd, $screenX, $screenY, -1200)
        Start-Sleep -Milliseconds 300
        [OtimizeDriver]::Scroll($hwnd, $screenX, $screenY, -1200)
        Start-Sleep -Milliseconds 300
        [OtimizeDriver]::Screenshot($hwnd, $Out)
        Write-Host "scrolled -> $Out"
    }

    default {
        Write-Error "Unknown action '$Action'. Valid: screenshot, navigate, scan, scroll"
        exit 1
    }
}
