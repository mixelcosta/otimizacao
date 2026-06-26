using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.App.Services;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public sealed class AchadoScanViewModel
{
    public string Icone       { get; init; } = "";
    public string Descricao   { get; init; } = "";
    public string CorIcone    { get; init; } = "#FFAA00";
    public string Acao        { get; init; } = "";
    public bool   TemAcao     => !string.IsNullOrEmpty(Acao);
}

public partial class HomeViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private readonly Action _navegarParaDashboard;
    private readonly Action<string>? _navegarParaOtimizador;
    private readonly Action? _navegarParaBios;

    private readonly Action<Inventario>? _onScanCompleto;
    private Inventario? _ultimoInventario;

    public HomeViewModel(
        IRoteadorIpc agente,
        Action navegarParaDashboard,
        Action<Inventario>? onScanCompleto = null,
        Action<string>? navegarParaOtimizador = null,
        Action? navegarParaBios = null)
    {
        _agente = agente;
        _navegarParaDashboard = navegarParaDashboard;
        _onScanCompleto = onScanCompleto;
        _navegarParaOtimizador = navegarParaOtimizador;
        _navegarParaBios = navegarParaBios;
        Achados = [];
    }

    // ── Achados pós-scan ───────────────────────────────────────────────────
    public ObservableCollection<AchadoScanViewModel> Achados { get; }
    [ObservableProperty] private int    _pontuacaoOtimizacao = 100;
    [ObservableProperty] private string _corPontuacao        = "#00C870";
    [ObservableProperty] private string _labelPontuacao      = "Sistema otimizado";

    // ── Scan state ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool _escaneando;
    [ObservableProperty] private bool _scanConcluido;
    [ObservableProperty] private string _statusExport = "";
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
            var resp = await Task.Run(async () => await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" }));
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

    [RelayCommand]
    private async Task ExportarRelatorioAsync()
    {
        if (_ultimoInventario is null) return;
        try
        {
            StatusExport = "Gerando relatório...";
            if (!OperatingSystem.IsWindows()) { StatusExport = "Exportação disponível apenas no Windows."; return; }
            var arquivo = await ServicoRelatorio.ExportarHtmlAsync(_ultimoInventario);
            StatusExport = $"Relatório salvo em: {Path.GetFileName(arquivo)}";
            // Abre no navegador padrão
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = arquivo,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusExport = $"Erro ao exportar: {ex.Message}";
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void AplicarResultados(Inventario inv)
    {
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

        GerarAchadosEPontuacao(inv);

        _ultimoInventario = inv;
        ProgressoScan     = 1.0;
        TextoBotaoScan    = "100%";
        SubtextoBotaoScan = "concluído";
        UltimoScanLabel   = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
        StatusText        = "Hardware detectado com sucesso";
        ScanConcluido     = true;
    }

    private void GerarAchadosEPontuacao(Inventario inv)
    {
        Achados.Clear();
        int score = 100;

        // Startup de alto impacto
        var startupAlto = inv.EntradasStartup
            .Where(e => e.Ativo && e.Impacto == ImpactoInicializacao.Alto)
            .ToList();
        if (startupAlto.Count > 0)
        {
            score -= Math.Min(20, startupAlto.Count * 7);
            Achados.Add(new AchadoScanViewModel
            {
                Icone     = "⚡",
                Descricao = $"{startupAlto.Count} programa{(startupAlto.Count > 1 ? "s" : "")} de alto impacto na inicialização",
                CorIcone  = "#FFAA00",
                Acao      = "inicializacao",
            });
        }

        // Bloatware instalado
        var bloatware = inv.ProgramasInstalados.Where(p => p.Bloatware).ToList();
        if (bloatware.Count > 0)
        {
            score -= Math.Min(15, bloatware.Count * 5);
            Achados.Add(new AchadoScanViewModel
            {
                Icone     = "🗑",
                Descricao = $"{bloatware.Count} programa{(bloatware.Count > 1 ? "s" : "")} identificado{(bloatware.Count > 1 ? "s" : "")} como bloatware",
                CorIcone  = "#FF6666",
                Acao      = "programas",
            });
        }

        // XMP/RAM rodando abaixo do potencial
        bool xmpDesativado = false;
        if (inv.Memoria.Count > 0)
        {
            var modulo = inv.Memoria.FirstOrDefault();
            if (modulo != null &&
                modulo.VelocidadeMhz.HasValue &&
                modulo.VelocidadeConfiguradaMhz.HasValue &&
                modulo.VelocidadeConfiguradaMhz.Value < modulo.VelocidadeMhz.Value * 0.9)
            {
                xmpDesativado = true;
                score -= 15;
                Achados.Add(new AchadoScanViewModel
                {
                    Icone     = "💾",
                    Descricao = $"RAM rodando a {modulo.VelocidadeConfiguradaMhz} MHz (potencial XMP: {modulo.VelocidadeMhz} MHz)",
                    CorIcone  = "#FFAA00",
                    Acao      = "bios",
                });
            }

            // Single Channel com apenas 1 pente
            if (inv.Memoria.Count == 1)
            {
                score -= 10;
                Achados.Add(new AchadoScanViewModel
                {
                    Icone     = "📊",
                    Descricao = "RAM em Single-Channel — adicionar um 2° pente duplica a largura de banda",
                    CorIcone  = "#8888CC",
                    Acao      = "",
                });
            }
        }

        // Secure Boot desativado
        if (inv.Placa.SecureBoot == false)
        {
            score -= 8;
            Achados.Add(new AchadoScanViewModel
            {
                Icone     = "🔓",
                Descricao = "Secure Boot está desativado — risco de segurança",
                CorIcone  = "#FF6666",
                Acao      = "bios",
            });
        }

        // Drivers desatualizados
        int driversDesatualizados = inv.Drivers.Count(d => d.Status == StatusDriver.AtualizacaoDisponivel);
        if (driversDesatualizados > 0)
        {
            score -= Math.Min(10, driversDesatualizados * 5);
            Achados.Add(new AchadoScanViewModel
            {
                Icone     = "🔧",
                Descricao = $"{driversDesatualizados} driver{(driversDesatualizados > 1 ? "s" : "")} desatualizado{(driversDesatualizados > 1 ? "s" : "")} detectado{(driversDesatualizados > 1 ? "s" : "")}",
                CorIcone  = "#FFAA00",
                Acao      = "",
            });
        }

        // Disco acima de 90%
        if (inv.Metricas?.Discos != null)
        {
            var discoCheio = inv.Metricas.Discos.FirstOrDefault(d => d.UsoPercent >= 90);
            if (discoCheio != null)
            {
                score -= 8;
                Achados.Add(new AchadoScanViewModel
                {
                    Icone     = "💿",
                    Descricao = $"Drive {discoCheio.Letra} com {discoCheio.UsoPercent}% de uso — pouco espaço livre",
                    CorIcone  = "#CC3333",
                    Acao      = "limpeza",
                });
            }
        }

        if (Achados.Count == 0)
        {
            Achados.Add(new AchadoScanViewModel
            {
                Icone     = "✓",
                Descricao = "Sistema bem otimizado — nenhum problema crítico encontrado",
                CorIcone  = "#00C870",
                Acao      = "",
            });
        }

        score = Math.Max(0, Math.Min(100, score));
        PontuacaoOtimizacao = score;
        CorPontuacao  = score >= 80 ? "#00C870" : score >= 55 ? "#FFAA00" : "#CC3333";
        LabelPontuacao = score >= 80 ? "Sistema otimizado" : score >= 55 ? "Melhorias disponíveis" : "Atenção necessária";
    }

    [RelayCommand]
    private void IrParaAcao(string acao)
    {
        switch (acao)
        {
            case "inicializacao":
            case "programas":
                _navegarParaOtimizador?.Invoke(acao);
                break;
            case "bios":
                _navegarParaBios?.Invoke();
                break;
        }
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
