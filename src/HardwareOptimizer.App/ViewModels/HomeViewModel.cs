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

    private readonly Action<Inventario>? _onScanCompleto;

    public HomeViewModel(IRoteadorIpc agente, Action navegarParaDashboard, Action<Inventario>? onScanCompleto = null)
    {
        _agente = agente;
        _navegarParaDashboard = navegarParaDashboard;
        _onScanCompleto = onScanCompleto;
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

    // ── Fases do scan (limite superior de progresso → rótulo exibido) ──────

    private static readonly (double Ate, string Fase, string Status)[] _fases =
    [
        (0.14, "hardware básico",     "Lendo CPU, memória e placa-mãe..."),
        (0.28, "S.M.A.R.T. discos",  "Verificando saúde dos discos..."),
        (0.44, "drivers",             "Identificando dispositivos e drivers..."),
        (0.58, "startup & serviços",  "Mapeando inicialização do Windows..."),
        (0.72, "programas",           "Listando programas instalados..."),
        (0.88, "arq. temporários",    "Analisando pastas temporárias..."),
    ];

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (Escaneando) return;

        Escaneando        = true;
        ProgressoScan     = 0;
        TextoBotaoScan    = "0%";
        SubtextoBotaoScan = _fases[0].Fase;
        StatusText        = _fases[0].Status;

        using var cts = new CancellationTokenSource();
        _ = AvançarProgressoAsync(cts.Token);

        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" });
            cts.Cancel();

            if (resp.Sucesso && resp.Resultado is Inventario inv)
            {
                AplicarResultados(inv);
                _onScanCompleto?.Invoke(inv);
            }
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
        TextoBotaoScan    = "100%";
        SubtextoBotaoScan = "concluído";
        UltimoScanLabel   = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
        StatusText        = "Hardware detectado com sucesso";
        ScanConcluido     = true;
    }

    private async Task AvançarProgressoAsync(CancellationToken ct)
    {
        // Preenche 0 → 88 % em ~15 s (passo de 0.01 a cada 170 ms).
        // O salto final para 100 % acontece em AplicarResultados().
        try
        {
            while (!ct.IsCancellationRequested && ProgressoScan < 0.88)
            {
                await Task.Delay(170, ct);
                if (ct.IsCancellationRequested) break;
                ProgressoScan = Math.Min(0.88, ProgressoScan + 0.01);

                int pct = (int)(ProgressoScan * 100);
                TextoBotaoScan = $"{pct}%";

                var fase = _fases.FirstOrDefault(f => ProgressoScan <= f.Ate);
                if (fase != default)
                {
                    SubtextoBotaoScan = fase.Fase;
                    StatusText        = fase.Status;
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
