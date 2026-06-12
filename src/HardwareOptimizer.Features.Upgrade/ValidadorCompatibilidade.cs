using System.Reflection;
using System.Text.Json;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Upgrade;

public sealed class ValidadorCompatibilidade
{
    private static readonly Lazy<CatalogoHardware> _catalogo =
        new(CarregarCatalogo);

    private readonly ILogger<ValidadorCompatibilidade> _log;

    public ValidadorCompatibilidade(ILogger<ValidadorCompatibilidade> log)
    {
        _log = log;
    }

    public ResultadoCompatibilidade Validar(Inventario atual, PecaSubstituta nova)
    {
        return nova.Tipo switch
        {
            TipoPecaUpgrade.Cpu => ValidarCpu(atual, nova),
            TipoPecaUpgrade.Gpu => ValidarGpu(atual, nova),
            TipoPecaUpgrade.Ram => ValidarRam(atual, nova),
            _ => new ResultadoCompatibilidade
            {
                Compativel = true,
                PecaModelo = nova.Modelo,
                Avisos = new[] { "Validação detalhada não disponível para este tipo de componente." },
            },
        };
    }

    private ResultadoCompatibilidade ValidarCpu(Inventario atual, PecaSubstituta nova)
    {
        var incompatibilidades = new List<string>();
        var avisos = new List<string>();

        var placaInfo = BuscarPlacaMae(atual.Placa.Modelo);
        if (placaInfo is not null && !string.IsNullOrEmpty(nova.Socket))
        {
            if (!placaInfo.Socket.Equals(nova.Socket, StringComparison.OrdinalIgnoreCase))
                incompatibilidades.Add(
                    $"Socket incompatível: placa-mãe usa {placaInfo.Socket}, processador requer {nova.Socket}.");
        }
        else if (placaInfo is null)
        {
            avisos.Add("Placa-mãe não encontrada no catálogo; compatibilidade de socket não verificada.");
        }

        if (nova.TdpW.HasValue && nova.TdpW.Value > 125)
            avisos.Add($"TDP alto ({nova.TdpW.Value}W) pode exigir cooler robusto.");

        double? ganho = null;
        var cpuAtualInfo = BuscarCpu(atual.Cpu.Nome);
        var cpuNovaInfo = BuscarCpu(nova.Modelo);
        if (cpuAtualInfo is not null && cpuNovaInfo is not null && cpuAtualInfo.Score > 0)
            ganho = Math.Round((cpuNovaInfo.Score - cpuAtualInfo.Score) / (double)cpuAtualInfo.Score * 100, 1);

        return new ResultadoCompatibilidade
        {
            Compativel = incompatibilidades.Count == 0,
            PecaModelo = nova.Modelo,
            Incompatibilidades = incompatibilidades,
            Avisos = avisos,
            GanhoEstimadoPercent = ganho,
        };
    }

    private ResultadoCompatibilidade ValidarGpu(Inventario atual, PecaSubstituta nova)
    {
        var incompatibilidades = new List<string>();
        var avisos = new List<string>();

        var gpuNovaInfo = BuscarGpu(nova.Modelo);
        var tdpGpu = nova.TdpW ?? gpuNovaInfo?.TdpW ?? 0;

        // Estimativa de TDP total do sistema (CPU + GPU + 150W para o restante)
        var cpuInfo = BuscarCpu(atual.Cpu.Nome);
        int tdpCpu = cpuInfo?.TdpW ?? 65;
        int consumoTotal = tdpCpu + tdpGpu + 150;

        int? headroom = null;

        var cpuAtualGpuScore = BuscarGpu(atual.Gpu.FirstOrDefault()?.Nome ?? string.Empty);
        var cpuAtualScore = BuscarCpu(atual.Cpu.Nome);

        double? ganho = null;
        if (cpuAtualGpuScore is not null && gpuNovaInfo is not null && cpuAtualGpuScore.Score > 0)
            ganho = Math.Round((gpuNovaInfo.Score - cpuAtualGpuScore.Score) / (double)cpuAtualGpuScore.Score * 100, 1);

        if (tdpGpu > 300)
            avisos.Add($"GPU com TDP de {tdpGpu}W: verifique se a fonte é de pelo menos {consumoTotal + 50}W.");

        return new ResultadoCompatibilidade
        {
            Compativel = incompatibilidades.Count == 0,
            PecaModelo = nova.Modelo,
            Incompatibilidades = incompatibilidades,
            Avisos = avisos,
            GanhoEstimadoPercent = ganho,
            ConsumoTotalEstimadoW = consumoTotal,
            HeadroomFonteW = headroom,
        };
    }

    private ResultadoCompatibilidade ValidarRam(Inventario atual, PecaSubstituta nova)
    {
        var incompatibilidades = new List<string>();
        var avisos = new List<string>();

        var placaInfo = BuscarPlacaMae(atual.Placa.Modelo);
        if (placaInfo is not null && !string.IsNullOrEmpty(nova.TipoDdr))
        {
            if (!placaInfo.Ddr.Equals(nova.TipoDdr, StringComparison.OrdinalIgnoreCase))
                incompatibilidades.Add(
                    $"Tipo de memória incompatível: placa usa {placaInfo.Ddr}, módulo é {nova.TipoDdr}.");

            if (nova.VelocidadeMhz.HasValue && nova.VelocidadeMhz.Value > placaInfo.MaxRamMhz)
                avisos.Add(
                    $"Velocidade {nova.VelocidadeMhz}MHz excede o máximo suportado ({placaInfo.MaxRamMhz}MHz); "
                    + "operará no perfil XMP reduzido.");
        }

        double? ganho = null;
        bool ehDualChannel = atual.Memoria.Count == 2 &&
            atual.Memoria.All(m => m.TamanhoGb == atual.Memoria[0].TamanhoGb);
        if (!ehDualChannel && atual.Memoria.Count == 1)
        {
            ganho = 15.0;
            avisos.Add("Adicionar um segundo pente igualariam o Dual-Channel: ganho estimado de ~15% em aplicações com uso intenso de memória.");
        }

        return new ResultadoCompatibilidade
        {
            Compativel = incompatibilidades.Count == 0,
            PecaModelo = nova.Modelo,
            Incompatibilidades = incompatibilidades,
            Avisos = avisos,
            GanhoEstimadoPercent = ganho,
        };
    }

    private static CatalogoHardware CarregarCatalogo()
    {
        var asm = Assembly.GetExecutingAssembly();
        const string res = "HardwareOptimizer.Features.Upgrade.Data.hardware_catalog.json";
        using var stream = asm.GetManifestResourceStream(res);
        if (stream is null) return new CatalogoHardware();
        return JsonSerializer.Deserialize<CatalogoHardware>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new CatalogoHardware();
    }

    private static EntradaCpu? BuscarCpu(string nome) =>
        _catalogo.Value.Processadores.FirstOrDefault(c =>
            nome.Contains(c.Modelo, StringComparison.OrdinalIgnoreCase)
            || c.Modelo.Contains(nome, StringComparison.OrdinalIgnoreCase));

    private static EntradaGpu? BuscarGpu(string nome) =>
        _catalogo.Value.Gpus.FirstOrDefault(g =>
            nome.Contains(g.Modelo, StringComparison.OrdinalIgnoreCase)
            || g.Modelo.Contains(nome, StringComparison.OrdinalIgnoreCase));

    private static EntradaPlacaMae? BuscarPlacaMae(string nome) =>
        _catalogo.Value.PlacasMae.FirstOrDefault(p =>
            nome.Contains(p.Modelo, StringComparison.OrdinalIgnoreCase)
            || p.Modelo.Contains(nome, StringComparison.OrdinalIgnoreCase));

    private sealed class CatalogoHardware
    {
        public List<EntradaCpu> Processadores { get; set; } = new();
        public List<EntradaGpu> Gpus { get; set; } = new();
        public List<EntradaPlacaMae> PlacasMae { get; set; } = new();
    }

    private sealed class EntradaCpu
    {
        public string Modelo { get; set; } = string.Empty;
        public string Socket { get; set; } = string.Empty;
        public int TdpW { get; set; }
        public int Nucleos { get; set; }
        public int Score { get; set; }
    }

    private sealed class EntradaGpu
    {
        public string Modelo { get; set; } = string.Empty;
        public string Interface { get; set; } = string.Empty;
        public int TdpW { get; set; }
        public int VramGb { get; set; }
        public int Score { get; set; }
    }

    private sealed class EntradaPlacaMae
    {
        public string Modelo { get; set; } = string.Empty;
        public string Socket { get; set; } = string.Empty;
        public List<string> Slots { get; set; } = new();
        public string Ddr { get; set; } = string.Empty;
        public int MaxRamMhz { get; set; }
        public int MaxRamGb { get; set; }
    }
}
