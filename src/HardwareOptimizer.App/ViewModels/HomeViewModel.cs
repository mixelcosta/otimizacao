using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private readonly Action _navegarParaDashboard;

    public HomeViewModel(IRoteadorIpc agente, Action navegarParaDashboard)
    {
        _agente = agente;
        _navegarParaDashboard = navegarParaDashboard;
    }

    // ── Scan state ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool _escaneando;
    [ObservableProperty] private bool _scanConcluido;
    [ObservableProperty] private double _progressoScan;

    // ── Scan button text ───────────────────────────────────────────────────

    [ObservableProperty] private string _textoBotaoScan    = "SCAN";
    [ObservableProperty] private string _subtextoBotaoScan = "detectar hardware";

    // ── Status labels ──────────────────────────────────────────────────────

    [ObservableProperty] private string _statusText      = "Pronto para escanear";
    [ObservableProperty] private string _ultimoScanLabel = "Último scan: nunca";

    // ── Left card (component count) ────────────────────────────────────────

    [ObservableProperty] private string _contadorDispositivos = "--";
    [ObservableProperty] private IBrush _corContador = new SolidColorBrush(Color.Parse("#3A3A3A"));

    // ── Right card (BIOS) ──────────────────────────────────────────────────

    [ObservableProperty] private string _iconeBios  = "--";
    [ObservableProperty] private string _statusBios = "aguardando";
    [ObservableProperty] private IBrush _corBios    = new SolidColorBrush(Color.Parse("#3A3A3A"));

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (Escaneando) return;

        Escaneando = true;
        ProgressoScan = 0;
        TextoBotaoScan    = "...";
        SubtextoBotaoScan = "detectando";
        StatusText        = "Escaneando hardware...";

        using var cts = new CancellationTokenSource();
        _ = AvançarProgressoAsync(cts.Token);

        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" });
            cts.Cancel();

            if (resp.Sucesso && resp.Resultado is Inventario inv)
                AplicarResultados(inv);
            else
            {
                StatusText        = "Falha ao detectar hardware";
                TextoBotaoScan    = "SCAN";
                SubtextoBotaoScan = "tentar novamente";
            }
        }
        finally
        {
            Escaneando = false;
        }
    }

    [RelayCommand]
    private void IrParaDashboard() => _navegarParaDashboard();

    // ── Helpers ────────────────────────────────────────────────────────────

    private void AplicarResultados(Inventario inv)
    {
        // Sum detected components: 1 CPU + GPUs + RAM sticks + 1 Mobo
        int total = 1 + inv.Gpu.Count + inv.Memoria.Count + 1;
        ContadorDispositivos = total.ToString();
        CorContador = new SolidColorBrush(Color.Parse("#00C870"));

        var bios = inv.Placa.VersaoBios;
        if (!string.IsNullOrWhiteSpace(bios))
        {
            IconeBios  = "✓";
            StatusBios = $"v{bios}";
            CorBios    = new SolidColorBrush(Color.Parse("#00FF88"));
        }
        else
        {
            IconeBios  = "?";
            StatusBios = "não detectado";
            CorBios    = new SolidColorBrush(Color.Parse("#FF8C00"));
        }

        ProgressoScan     = 1.0;
        UltimoScanLabel   = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
        StatusText        = "Hardware detectado com sucesso";
        TextoBotaoScan    = "SCAN";
        SubtextoBotaoScan = "escanear novamente";
        ScanConcluido     = true;
    }

    private async Task AvançarProgressoAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ProgressoScan < 0.88)
            {
                await Task.Delay(110, ct);
                ProgressoScan = Math.Min(0.88, ProgressoScan + 0.04);
            }
        }
        catch (OperationCanceledException) { }
    }
}
