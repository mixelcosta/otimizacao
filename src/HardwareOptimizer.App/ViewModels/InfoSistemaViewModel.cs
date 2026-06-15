using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class InfoSistemaViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public InfoSistemaViewModel(IRoteadorIpc agente) => _agente = agente;

    // Placa-mãe
    [ObservableProperty] private string _fabricante = "–";
    [ObservableProperty] private string _modelo = "–";
    [ObservableProperty] private string _busSpecs = "–";
    [ObservableProperty] private string _chipset = "–";

    // CPU
    [ObservableProperty] private string _nomeCpu = "–";
    [ObservableProperty] private string _fabricanteCpu = "–";
    [ObservableProperty] private string _soqueteCpu = "–";
    [ObservableProperty] private string _nucleosCpu = "–";
    [ObservableProperty] private string _clockBaseCpu = "–";
    [ObservableProperty] private string _clockAtualCpu = "–";
    [ObservableProperty] private string _cacheL2Cpu = "–";
    [ObservableProperty] private string _cacheL3Cpu = "–";

    // BIOS
    [ObservableProperty] private string _fabricanteBios = "–";
    [ObservableProperty] private string _versaoBios = "–";
    [ObservableProperty] private string _dataBios = "–";
    [ObservableProperty] private string _modoBios = "–";

    // GPU / Interface PCIe
    [ObservableProperty] private string _nomeGpu = "–";
    [ObservableProperty] private string _linkWidthAtual = "–";
    [ObservableProperty] private string _linkWidthMax = "x16";
    [ObservableProperty] private string _linkSpeedAtual = "–";
    [ObservableProperty] private string _linkSpeedMax = "–";

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _erro = string.Empty;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        Carregando = true;
        Erro = string.Empty;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" });
            if (!resp.Sucesso || resp.Resultado is not Inventario inv)
            {
                Erro = "Falha ao coletar dados do sistema.";
                return;
            }

            // CPU
            var cpu = inv.Cpu;
            NomeCpu       = cpu.Nome;
            FabricanteCpu = cpu.Fabricante ?? "–";
            SoqueteCpu    = cpu.Soquete ?? "–";
            NucleosCpu    = (cpu.Nucleos.HasValue && cpu.Threads.HasValue)
                ? $"{cpu.Nucleos} núcleos  /  {cpu.Threads} threads"
                : cpu.Nucleos.HasValue ? $"{cpu.Nucleos} núcleos" : "–";
            ClockBaseCpu  = cpu.ClockBaseMhz.HasValue
                ? FormatarClock(cpu.ClockBaseMhz.Value) : "–";
            ClockAtualCpu = cpu.ClockAtualMhz.HasValue
                ? FormatarClock(cpu.ClockAtualMhz.Value) : "–";
            CacheL2Cpu    = cpu.L2CacheKb.HasValue
                ? FormatarCache(cpu.L2CacheKb.Value) : "–";
            CacheL3Cpu    = cpu.L3CacheKb.HasValue
                ? FormatarCache(cpu.L3CacheKb.Value) : "–";

            // Placa-mãe
            Fabricante = inv.Placa.Fabricante;
            Modelo = inv.Placa.Modelo;
            BusSpecs = inv.Placa.BusSpecs ?? "–";
            Chipset = inv.Placa.Chipset ?? "–";
            VersaoBios = inv.Placa.VersaoBios ?? "–";
            DataBios = inv.Placa.DataBios ?? "–";
            ModoBios = inv.Placa.Modo ?? "–";
            FabricanteBios = InferirFabricanteBios(inv.Placa.Fabricante);

            if (inv.Gpu.Count > 0)
            {
                var gpu = inv.Gpu[0];
                NomeGpu = gpu.Nome;
                LinkWidthAtual = gpu.LinkWidthAtual ?? "–";
                LinkWidthMax = gpu.LinkWidthMax ?? "x16";
                LinkSpeedAtual = gpu.LinkSpeedAtual ?? "–";
                LinkSpeedMax = gpu.LinkSpeedMax ?? DerivarSpeedMaxDoChipset(inv.Placa.BusSpecs);
            }
        }
        finally { Carregando = false; }
    }

    private static string FormatarClock(int mhz) =>
        mhz >= 1000 ? $"{mhz / 1000.0:F2} GHz" : $"{mhz} MHz";

    private static string FormatarCache(int kb) =>
        kb >= 1024 ? $"{kb / 1024} MB" : $"{kb} KB";

    private static string InferirFabricanteBios(string fabricante)
    {
        if (fabricante.Contains("ASUS", StringComparison.OrdinalIgnoreCase) ||
            fabricante.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) ||
            fabricante.Contains("ASRock", StringComparison.OrdinalIgnoreCase) ||
            fabricante.Contains("MSI", StringComparison.OrdinalIgnoreCase))
            return "American Megatrends Inc.";
        return "–";
    }

    private static string DerivarSpeedMaxDoChipset(string? busSpecs)
    {
        if (busSpecs == null) return "–";
        if (busSpecs.Contains("5.0")) return "32.0 GT/s";
        if (busSpecs.Contains("4.0")) return "16.0 GT/s";
        if (busSpecs.Contains("3.0")) return "8.0 GT/s";
        return "–";
    }
}
