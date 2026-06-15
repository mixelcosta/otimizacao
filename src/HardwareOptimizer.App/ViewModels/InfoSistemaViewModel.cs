using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class InfoSistemaViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public InfoSistemaViewModel(IRoteadorIpc agente) => _agente = agente;

    // CPU
    [ObservableProperty] private string _nomeCpu = "–";
    [ObservableProperty] private string _fabricanteCpu = "–";
    [ObservableProperty] private string _soqueteCpu = "–";
    [ObservableProperty] private string _nucleosCpu = "–";
    [ObservableProperty] private string _clockBaseCpu = "–";
    [ObservableProperty] private string _clockAtualCpu = "–";
    [ObservableProperty] private string _cacheL2Cpu = "–";
    [ObservableProperty] private string _cacheL3Cpu = "–";

    // RAM — resumo
    [ObservableProperty] private string _resumoRam = "–";
    public ObservableCollection<SlotRamVm> SlotsRam { get; } = [];

    // GPU
    [ObservableProperty] private string _nomeGpu = "–";
    [ObservableProperty] private string _vramGpu = "–";
    [ObservableProperty] private string _resolucaoGpu = "–";
    [ObservableProperty] private string _taxaGpu = "–";
    [ObservableProperty] private string _driverGpu = "–";
    [ObservableProperty] private string _dataDriverGpu = "–";
    [ObservableProperty] private string _linkWidthAtual = "–";
    [ObservableProperty] private string _linkWidthMax = "x16";
    [ObservableProperty] private string _linkSpeedAtual = "–";
    [ObservableProperty] private string _linkSpeedMax = "–";

    // Placa-mãe
    [ObservableProperty] private string _fabricante = "–";
    [ObservableProperty] private string _modelo = "–";
    [ObservableProperty] private string _busSpecs = "–";
    [ObservableProperty] private string _chipset = "–";

    // BIOS
    [ObservableProperty] private string _fabricanteBios = "–";
    [ObservableProperty] private string _versaoBios = "–";
    [ObservableProperty] private string _dataBios = "–";
    [ObservableProperty] private string _modoBios = "–";

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
            ClockBaseCpu  = cpu.ClockBaseMhz.HasValue  ? FormatarClock(cpu.ClockBaseMhz.Value)  : "–";
            ClockAtualCpu = cpu.ClockAtualMhz.HasValue ? FormatarClock(cpu.ClockAtualMhz.Value) : "–";
            CacheL2Cpu    = cpu.L2CacheKb.HasValue     ? FormatarCache(cpu.L2CacheKb.Value)     : "–";
            CacheL3Cpu    = cpu.L3CacheKb.HasValue     ? FormatarCache(cpu.L3CacheKb.Value)     : "–";

            // RAM
            SlotsRam.Clear();
            if (inv.Memoria.Count > 0)
            {
                var totalGb  = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
                var tipo     = inv.Memoria.FirstOrDefault(m => m.Tipo != null)?.Tipo ?? "DDR";
                var velConf  = inv.Memoria.FirstOrDefault(m => m.VelocidadeConfiguradaMhz > 0)?.VelocidadeConfiguradaMhz
                            ?? inv.Memoria.FirstOrDefault(m => m.VelocidadeMhz > 0)?.VelocidadeMhz;
                var canais   = inv.Memoria.Count >= 2 ? "Dual-Channel" : "Single-Channel";
                ResumoRam = velConf.HasValue
                    ? $"{totalGb} GB {tipo}-{velConf}  ·  {canais}"
                    : $"{totalGb} GB {tipo}  ·  {canais}";

                foreach (var m in inv.Memoria)
                    SlotsRam.Add(new SlotRamVm(m));
            }

            // GPU
            if (inv.Gpu.Count > 0)
            {
                var gpu = inv.Gpu[0];
                NomeGpu        = gpu.Nome;
                VramGpu        = gpu.VramMb.HasValue ? FormatarVram(gpu.VramMb.Value) : "–";
                ResolucaoGpu   = gpu.Resolucao ?? "–";
                TaxaGpu        = gpu.TaxaAtualizacaoHz.HasValue ? $"{gpu.TaxaAtualizacaoHz} Hz" : "–";
                DriverGpu      = gpu.VersaoDriver ?? "–";
                DataDriverGpu  = gpu.DataDriver ?? "–";
                LinkWidthAtual = gpu.LinkWidthAtual ?? "–";
                LinkWidthMax   = gpu.LinkWidthMax ?? "x16";
                LinkSpeedAtual = gpu.LinkSpeedAtual ?? "–";
                LinkSpeedMax   = gpu.LinkSpeedMax ?? DerivarSpeedMaxDoChipset(inv.Placa.BusSpecs);
            }

            // Placa-mãe
            Fabricante     = inv.Placa.Fabricante;
            Modelo         = inv.Placa.Modelo;
            BusSpecs       = inv.Placa.BusSpecs ?? "–";
            Chipset        = inv.Placa.Chipset ?? "–";
            VersaoBios     = inv.Placa.VersaoBios ?? "–";
            DataBios       = inv.Placa.DataBios ?? "–";
            ModoBios       = inv.Placa.Modo ?? "–";
            FabricanteBios = InferirFabricanteBios(inv.Placa.Fabricante);
        }
        finally { Carregando = false; }
    }

    private static string FormatarClock(int mhz) =>
        mhz >= 1000 ? $"{mhz / 1000.0:F2} GHz" : $"{mhz} MHz";

    private static string FormatarCache(int kb) =>
        kb >= 1024 ? $"{kb / 1024} MB" : $"{kb} KB";

    private static string FormatarVram(int mb) =>
        mb >= 1024 ? $"{mb / 1024} GB" : $"{mb} MB";

    private static string InferirFabricanteBios(string fabricante)
    {
        if (fabricante.Contains("ASUS",     StringComparison.OrdinalIgnoreCase) ||
            fabricante.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) ||
            fabricante.Contains("ASRock",   StringComparison.OrdinalIgnoreCase) ||
            fabricante.Contains("MSI",      StringComparison.OrdinalIgnoreCase))
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

public sealed class SlotRamVm
{
    public SlotRamVm(ModuloMemoria m)
    {
        Slot      = m.Slot ?? "–";
        Tamanho   = m.TamanhoGb.HasValue ? $"{m.TamanhoGb} GB" : "–";
        TipoVel   = FormatarTipoVel(m);
        Fabricante = m.Fabricante ?? "–";
        Modelo    = m.Modelo?.Trim() ?? "–";
        FormFactor = m.FormFactor ?? "–";
        Tensao    = m.Tensao ?? "–";
    }

    public string Slot       { get; }
    public string Tamanho    { get; }
    public string TipoVel    { get; }
    public string Fabricante { get; }
    public string Modelo     { get; }
    public string FormFactor { get; }
    public string Tensao     { get; }

    private static string FormatarTipoVel(ModuloMemoria m)
    {
        var tipo = m.Tipo ?? "DDR";
        var vel  = m.VelocidadeConfiguradaMhz ?? m.VelocidadeMhz;
        return vel.HasValue ? $"{tipo}-{vel}" : tipo;
    }
}
