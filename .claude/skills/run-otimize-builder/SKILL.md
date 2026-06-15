---
name: run-otimize-builder
description: Run, screenshot, scan, and navigate the Otimize Builder desktop app (Avalonia/.NET 8, Windows). Use when asked to start, launch, run, screenshot, test, or demonstrate the app.
---

# run-otimize-builder

Otimize Builder is a Windows-only Avalonia desktop app for hardware optimization. It is driven via Win32 `PostMessage` + `PrintWindow` — no Playwright, no cursor movement. The driver script is `.claude/skills/run-otimize-builder/driver.ps1`.

**All commands run from the repo root** (`C:\Users\Michel\Documents\GitHub\otimizacao`).

---

## Prerequisites

- .NET 8 SDK (`dotnet --version` → 8.x)
- Windows (Win32 APIs required)
- Named pipe agent must be running as part of the app (launched automatically by `HardwareOptimizer.App`)
- PowerShell 5.1 (built-in on Windows — do NOT use `using var` syntax in `Add-Type` blocks; it fails; use explicit type declarations)

---

## Build

```powershell
dotnet build src/HardwareOptimizer.App --configuration Release
```

Expect: `Compilação com êxito. 0 Aviso(s) 0 Erro(s)`

---

## Run (agent path)

### 1. Launch the app (background)

```powershell
$proc = Start-Process dotnet -ArgumentList "run","--project","src/HardwareOptimizer.App","--configuration","Release","--no-build" -PassThru
Start-Sleep -Seconds 7   # wait for Avalonia + named pipe to initialize
```

### 2. Drive it with driver.ps1

All interactions are one PowerShell call (Add-Type state does not persist between calls):

```powershell
# Take a screenshot of the current screen
.\driver.ps1 -Action screenshot -Out C:\Temp\shot.png

# Navigate to a sidebar section (then screenshot)
.\driver.ps1 -Action navigate -Target InfoSistema -Out C:\Temp\info.png

# Click SCAN NOW and wait 12s for hardware detection
.\driver.ps1 -Action scan -Delay 12000 -Out C:\Temp\post_scan.png

# Scroll down the current page and screenshot
.\driver.ps1 -Action scroll -Out C:\Temp\scrolled.png
```

**Valid `-Target` values:** `Dashboard`, `OtimizadorWindows`, `InfoSistema`, `IaCopiloto`, `Configuracoes`

### 3. Typical full flow (Home → SCAN → Info Sistema)

```powershell
$proc = Start-Process dotnet -ArgumentList "run","--project","src/HardwareOptimizer.App","--configuration","Release","--no-build" -PassThru
Start-Sleep -Seconds 7

# Home screen (pre-scan)
.\driver.ps1 -Action screenshot -Out C:\Temp\home.png

# Run hardware detection (~10–12s)
.\driver.ps1 -Action scan -Delay 12000 -Out C:\Temp\scan_done.png

# Navigate to Info Sistema (populated by SCAN callback)
.\driver.ps1 -Action navigate -Target InfoSistema -Out C:\Temp\info_top.png

# Scroll to see RAM + GPU panels
.\driver.ps1 -Action scroll -Out C:\Temp\info_bottom.png

# Kill app when done
Stop-Process -Id $proc.Id -Force
```

---

## Run (human path)

```powershell
dotnet run --project src/HardwareOptimizer.App --configuration Release
```

Window titled "Otimize Builder" opens. Click SCAN in the center circle to detect hardware. Use the sidebar to navigate.

---

## Screenshot internals

`PrintWindow` is called with `nFlags=2` (`PW_RENDERFULLCONTENT`). This is required for Avalonia GPU-composited content — `nFlags=0` returns a blank bitmap. The client rect (1100×700 at DPI=96) is what's captured, not the full window (title bar excluded).

---

## Coordinate system

- **Screenshot pixels** = visual coords relative to top-left of client area (title bar excluded)
- **PostMessage click coords** = same client coords (`lParam = (y << 16) | x`)
- **Title bar height** = `WinRect.Height - ClientRect.Height` = **39 px** at DPI=96/100%
- **WM_MOUSEWHEEL lParam** = screen-absolute coords (add `WinRect.L` and `WinRect.T + titleBarH`)

### Sidebar client coordinates (DPI=96)

| Page | client X | client Y |
|---|---|---|
| Dashboard | 97 | 143 |
| Otimizador Windows | 97 | 186 |
| Info Sistema | 97 | 189 |
| IA Copiloto | 97 | 231 |
| Configurações | 97 | 671 |

### SCAN button

Center of scan circle: **client (652, 361)**

---

## Gotchas

- **`using var` fails in PowerShell 5.1 Add-Type**: PowerShell 5.1 compiles with an older C# language version. Replace `using var x = …` with `Type x = new Type(…); … x.Dispose();` — or just skip Dispose for GDI objects (bitmaps save first).
- **Add-Type types don't persist between PowerShell tool calls**: Define the type and use it in the same `Add-Type` + invocation block. A type defined in call A is gone in call B.
- **nFlags=0 returns blank**: Avalonia uses hardware composition. `PrintWindow(hwnd, hdc, 0)` captures a blank frame. Always pass `2`.
- **SetCursorPos + mouse_event doesn't work on secondary monitor with negative X**: Window at X=-1924 (secondary monitor left of primary) ignores synthetic cursor events. Use `PostMessage WM_LBUTTONDOWN` instead — it doesn't move the cursor.
- **Info Sistema shows "–" for Bus Specs on X570**: WMI returns "PCI standard host CPU bridge" instead of chipset marketing name. Known limitation of WMI on AMD X570.
- **PCIe Link Width/Speed shows "–" for AMD RX 7800 XT**: `DEVPKEY_PciDevice` data not returned by the driver for this GPU model.
- **VRAM shows 3 GB for RX 7800 XT**: WMI `AdapterRAM` is inaccurate for this GPU (actual is 16 GB). Requires DXGI or registry fallback, not yet implemented.
- **Hardware scan takes 10–12 seconds**: The named pipe call to `HardwareOptimizer.Agent` performs WMI queries across CPU/RAM/GPU/Mobo. Use `-Delay 12000` on the `scan` action.
- **Scroll uses screen-absolute coords in lParam**: `WM_MOUSEWHEEL` lParam encodes screen position, not client position. Driver auto-converts using `GetWindowRect`.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `window not found` | App hasn't started yet — increase `Start-Sleep` after launch |
| Screenshot is all black | Used `PrintWindow` with `nFlags=0` — change to `2` |
| Click navigates to wrong panel | Verify DPI is 96 (100% scaling); coordinates shift at 125%/150% |
| `Não é possível localizar o tipo [OtimizeDriver]` | Add-Type and usage split across two PowerShell calls — combine into one |
| Named pipe error on scan | Agent didn't start. Restart the app; the agent is launched in-process |
| Build fails | Run `dotnet restore` first, then `dotnet build` |
