using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.ViewModels;

public partial class InfoSistemaViewModel : ObservableObject
{
    // Sistema Operacional
    [ObservableProperty] private string _nomeOs = "–";
    [ObservableProperty] private string _versaoOs = "–";
    [ObservableProperty] private string _arquiteturaOs = "–";

    // CPU
    [ObservableProperty] private string _nomeCpu = "–";
    [ObservableProperty] private string _fabricanteCpu = "–";
    [ObservableProperty] private string _soqueteCpu = "–";
    [ObservableProperty] private string _nucleosCpu = "–";
    [ObservableProperty] private string _clockBaseCpu = "–";
    [ObservableProperty] private string _clockAtualCpu = "–";
    [ObservableProperty] private string _cacheL2Cpu = "–";
    [ObservableProperty] private string _cacheL3Cpu = "–";
    [ObservableProperty] private string _tempIdleCpu = "–";

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
    [ObservableProperty] private string _tempIdleGpu = "–";

    // Placa-mãe
    [ObservableProperty] private string _fabricante = "–";
    [ObservableProperty] private string _modelo = "–";
    [ObservableProperty] private string _busSpecs = "–";
    [ObservableProperty] private string _chipset = "–";
    [ObservableProperty] private string _secureBoot = "–";

    // BIOS
    [ObservableProperty] private string _fabricanteBios = "–";
    [ObservableProperty] private string _versaoBios = "–";
    [ObservableProperty] private string _dataBios = "–";
    [ObservableProperty] private string _modoBios = "–";

    // Armazenamento
    public ObservableCollection<DadosDiscoVm> DiscosSistema { get; } = [];
    [ObservableProperty] private bool _temDiscos;

    // Rede
    public ObservableCollection<InterfaceRedeVm> InterfacesRede { get; } = [];
    [ObservableProperty] private bool _temRede;

    // S.M.A.R.T.
    public ObservableCollection<SaudeDiscoVm> SaudeDiscosSmart { get; } = [];
    [ObservableProperty] private bool _temSaudeDiscos;

    [ObservableProperty] private bool _comDados;

    public void Popular(Inventario inv)
    {
        // Sistema Operacional
        NomeOs        = inv.SistemaOperacional.Nome ?? "Windows";
        VersaoOs      = inv.SistemaOperacional.Versao ?? "–";
        ArquiteturaOs = inv.SistemaOperacional.Arquitetura ?? "–";

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
        TempIdleCpu   = cpu.TempIdleC.HasValue     ? $"{cpu.TempIdleC:F1} °C"               : "–";

        // RAM
        SlotsRam.Clear();
        if (inv.Memoria.Count > 0)
        {
            var totalGb = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
            var tipo    = inv.Memoria.FirstOrDefault(m => m.Tipo != null)?.Tipo ?? "DDR";
            var vel     = inv.Memoria.FirstOrDefault(m => m.VelocidadeConfiguradaMhz > 0)?.VelocidadeConfiguradaMhz
                       ?? inv.Memoria.FirstOrDefault(m => m.VelocidadeMhz > 0)?.VelocidadeMhz;
            var canais  = inv.Memoria.Count >= 2 ? "Dual-Channel" : "Single-Channel";
            ResumoRam   = vel.HasValue ? $"{totalGb} GB {tipo}-{vel}  ·  {canais}" : $"{totalGb} GB {tipo}  ·  {canais}";

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
            TempIdleGpu    = gpu.TempIdleC.HasValue ? $"{gpu.TempIdleC:F1} °C" : "–";
        }

        // Placa-mãe
        Fabricante  = inv.Placa.Fabricante;
        Modelo      = inv.Placa.Modelo;
        BusSpecs    = inv.Placa.BusSpecs ?? "–";
        Chipset     = inv.Placa.Chipset ?? "–";
        SecureBoot  = inv.Placa.SecureBoot.HasValue
            ? (inv.Placa.SecureBoot.Value ? "Ativado" : "Desativado")
            : "–";

        // BIOS
        VersaoBios     = inv.Placa.VersaoBios ?? "–";
        DataBios       = inv.Placa.DataBios ?? "–";
        ModoBios       = inv.Placa.Modo ?? "–";
        FabricanteBios = InferirFabricanteBios(inv.Placa.Fabricante);

        // Armazenamento
        DiscosSistema.Clear();
        if (inv.Metricas?.Discos != null)
        {
            foreach (var d in inv.Metricas.Discos)
                DiscosSistema.Add(new DadosDiscoVm(d));
        }
        TemDiscos = DiscosSistema.Count > 0;

        // Rede
        InterfacesRede.Clear();
        foreach (var r in inv.Rede)
            InterfacesRede.Add(new InterfaceRedeVm(r));
        TemRede = InterfacesRede.Count > 0;

        // S.M.A.R.T.
        SaudeDiscosSmart.Clear();
        foreach (var s in inv.SaudeDiscos)
            SaudeDiscosSmart.Add(new SaudeDiscoVm(s));
        TemSaudeDiscos = SaudeDiscosSmart.Count > 0;

        ComDados = true;
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
        Slot       = m.Slot ?? "–";
        Tamanho    = m.TamanhoGb.HasValue ? $"{m.TamanhoGb} GB" : "–";
        TipoVel    = FormatarTipoVel(m);
        Fabricante = m.Fabricante ?? "–";
        Modelo     = m.Modelo?.Trim() ?? "–";
        FormFactor = m.FormFactor ?? "–";
        Tensao     = m.Tensao ?? "–";
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

public sealed class DadosDiscoVm
{
    public DadosDiscoVm(MetricaDisco d)
    {
        Letra      = d.Letra;
        Total      = $"{d.TotalGb} GB";
        Usado      = $"{d.UsadoGb} GB";
        Livre      = $"{d.LivreGb} GB";
        UsoPercent = $"{d.UsoPercent}%";
        CorUso     = d.UsoPercent >= 90 ? "#CC3333"
                   : d.UsoPercent >= 75 ? "#FFAA00"
                   : "#00C870";
    }

    public string Letra      { get; }
    public string Total      { get; }
    public string Usado      { get; }
    public string Livre      { get; }
    public string UsoPercent { get; }
    public string CorUso     { get; }
}

public sealed class InterfaceRedeVm
{
    public InterfaceRedeVm(InterfaceRede r)
    {
        Nome = r.Nome;
        Tipo = r.Tipo ?? "–";
        Mac  = r.EnderecoMac ?? "–";
    }

    public string Nome { get; }
    public string Tipo { get; }
    public string Mac  { get; }
}

public sealed class SaudeDiscoVm
{
    public SaudeDiscoVm(SaudeDisco s)
    {
        Modelo        = s.Modelo;
        Letra         = s.Letra;
        VidaRestante  = $"{s.PorcentagemVidaRestante:F0}%";
        HorasUso      = s.HorasUso > 0 ? $"{s.HorasUso:N0} h" : "–";
        TbwEscrito    = s.TbwEscritoGb > 0 ? $"{s.TbwEscritoGb} GB" : "–";
        Status        = s.Nivel switch
        {
            NivelSaudeDisco.Bom     => "Bom",
            NivelSaudeDisco.Atencao => "Atenção",
            NivelSaudeDisco.Critico => "Crítico",
            _ => "–"
        };
        CorStatus     = s.Nivel switch
        {
            NivelSaudeDisco.Bom     => "#00C870",
            NivelSaudeDisco.Atencao => "#FFAA00",
            NivelSaudeDisco.Critico => "#CC3333",
            _ => "#555555"
        };
        Erros    = s.TemErrosNaoCorrigiveis ? "Sim" : "Não";
        CorErros = s.TemErrosNaoCorrigiveis ? "#CC3333" : "#555555";
        Setores  = s.SetoresComProblema > 0 ? s.SetoresComProblema.ToString() : "–";
    }

    public string Modelo       { get; }
    public string Letra        { get; }
    public string VidaRestante { get; }
    public string HorasUso     { get; }
    public string TbwEscrito   { get; }
    public string Status       { get; }
    public string CorStatus    { get; }
    public string Erros        { get; }
    public string CorErros     { get; }
    public string Setores      { get; }
}
