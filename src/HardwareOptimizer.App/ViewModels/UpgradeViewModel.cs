using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Upgrade;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class UpgradeViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private readonly CalculadoraGargalo _calc = new();

    public UpgradeViewModel(IRoteadorIpc agente) => _agente = agente;

    // ── State ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    private bool _carregando;

    [ObservableProperty] private bool _carregado;

    public string TextoBotao => Carregando ? "Detectando..." : "  Detectar Hardware  ";

    // ── Parts list ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _nomeCpu        = "–";
    [ObservableProperty] private string _infosCpu       = "";
    [ObservableProperty] private string _nomeGpu        = "–";
    [ObservableProperty] private string _infosGpu       = "";
    [ObservableProperty] private string _nomeRam        = "–";
    [ObservableProperty] private string _nomePlacaMae   = "–";
    [ObservableProperty] private string _infosPlaca     = "";
    [ObservableProperty] private string _nomeOs         = "–";

    // ── Bottleneck ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGargalo))]
    private string _componenteLimitante = "";

    [ObservableProperty] private string _gargaloLabel      = "";
    [ObservableProperty] private string _gargaloDescricao  = "";

    public bool IsGargalo =>
        ComponenteLimitante.Equals("CPU", StringComparison.OrdinalIgnoreCase) ||
        ComponenteLimitante.Equals("GPU", StringComparison.OrdinalIgnoreCase);

    // ── Diagram visibility ─────────────────────────────────────────────────

    [ObservableProperty] private bool _temCpu;
    [ObservableProperty] private bool _temGpu;
    [ObservableProperty] private bool _temRam;
    [ObservableProperty] private bool _temPlaca;

    // ── Command ────────────────────────────────────────────────────────────

    public async Task AtivarAsync()
    {
        if (!Carregado) await CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (Carregando) return;
        Carregando = true;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" });
            if (!resp.Sucesso || resp.Resultado is not Inventario inv) return;

            PopularCpu(inv);
            PopularGpu(inv);
            PopularRam(inv);
            PopularPlaca(inv);

            NomeOs = inv.SistemaOperacional.Nome ?? inv.SistemaOperacional.Tipo.ToString();

            AnalisarGargalo(inv);

            Carregado = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void PopularCpu(Inventario inv)
    {
        NomeCpu  = inv.Cpu.Nome;
        InfosCpu = inv.Cpu.Nucleos.HasValue
            ? $"{inv.Cpu.Nucleos} núcleos · {inv.Cpu.Threads} threads"
            : "";
        TemCpu = true;
    }

    private void PopularGpu(Inventario inv)
    {
        if (inv.Gpu.Count == 0) return;
        var gpu   = inv.Gpu[0];
        NomeGpu   = gpu.Nome;
        InfosGpu  = gpu.VersaoDriver is { } drv ? $"Driver {drv}" : "";
        TemGpu    = true;
    }

    private void PopularRam(Inventario inv)
    {
        if (inv.Memoria.Count == 0) return;

        var totalGb  = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
        var freqMhz  = inv.Memoria.FirstOrDefault()?.VelocidadeMhz;
        var qtd      = inv.Memoria.Count;
        var perStick = qtd > 0 ? totalGb / qtd : 0;
        var isDdr5   = freqMhz.HasValue && freqMhz.Value >= 4800;

        NomeRam = totalGb > 0
            ? $"{totalGb} GB DDR{(isDdr5 ? "5" : "4")}{(freqMhz > 0 ? $"-{freqMhz}" : "")} ({qtd}×{perStick} GB)"
            : "–";

        TemRam = totalGb > 0;
    }

    private void PopularPlaca(Inventario inv)
    {
        NomePlacaMae = $"{inv.Placa.Fabricante} {inv.Placa.Modelo}";
        InfosPlaca   = inv.Placa.VersaoBios is { } bios ? $"BIOS {bios}" : "";
        TemPlaca     = true;
    }

    private void AnalisarGargalo(Inventario inv)
    {
        var g = _calc.Calcular(inv);
        ComponenteLimitante = g.ComponenteLimitante;
        GargaloDescricao    = g.Descricao;
        GargaloLabel = g.ComponenteLimitante switch
        {
            "CPU" => $"⚠  Gargalo: CPU  (+{g.GanhoEstimadoPercent:F0}% estimado)",
            "GPU" => $"⚠  Gargalo: GPU  (+{g.GanhoEstimadoPercent:F0}% estimado)",
            _     => "✓  Setup Balanceado",
        };
    }
}
