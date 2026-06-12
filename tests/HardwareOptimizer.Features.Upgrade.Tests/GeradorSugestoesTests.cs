using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HardwareOptimizer.Features.Upgrade.Tests;

public sealed class GeradorSugestoesTests
{
    private static GeradorSugestoes CriarGerador() => new(
        new ValidadorCompatibilidade(NullLogger<ValidadorCompatibilidade>.Instance),
        new CalculadoraGargalo(),
        NullLogger<GeradorSugestoes>.Instance);

    private static Inventario InventarioSingleRam() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "AMD Ryzen 5 3600", Nucleos = 6 },
        Gpu = new[] { new PlacaVideo { Nome = "NVIDIA GeForce RTX 4090" } }, // força gargalo CPU
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 8, VelocidadeMhz = 3200 } }.ToList(),
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    private static Inventario InventarioGpuGargalo() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "AMD Ryzen 9 5900X", Nucleos = 12 },
        Gpu = new[] { new PlacaVideo { Nome = "NVIDIA GeForce RTX 4060" } }, // GPU mais fraca
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 },
                          new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } }.ToList(),
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public void CustoBeneficio_com_single_ram_inclui_sugestao_dual_channel()
    {
        var sugestoes = CriarGerador().Sugerir(InventarioSingleRam(), ModoSugestao.CustoBeneficio);

        Assert.NotEmpty(sugestoes);
        Assert.Contains(sugestoes, s => s.Peca.Tipo == TipoPecaUpgrade.Ram);
    }

    [Fact]
    public void CustoBeneficio_com_gpu_como_gargalo_sugere_rtx4070()
    {
        var sugestoes = CriarGerador().Sugerir(InventarioGpuGargalo(), ModoSugestao.CustoBeneficio);

        Assert.Contains(sugestoes, s =>
            s.Peca.Tipo == TipoPecaUpgrade.Gpu &&
            s.Peca.Modelo.Contains("4070", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HighEnd_sempre_inclui_cpu_e_gpu_highend()
    {
        var sugestoes = CriarGerador().Sugerir(InventarioGpuGargalo(), ModoSugestao.HighEnd);

        Assert.Contains(sugestoes, s => s.Peca.Tipo == TipoPecaUpgrade.Cpu);
        Assert.Contains(sugestoes, s => s.Peca.Tipo == TipoPecaUpgrade.Gpu);
    }

    [Fact]
    public void Toda_sugestao_contem_justificativa()
    {
        var sugestoes = CriarGerador().Sugerir(InventarioGpuGargalo(), ModoSugestao.HighEnd);

        Assert.All(sugestoes, s => Assert.NotEmpty(s.Justificativa));
    }

    [Fact]
    public void Toda_sugestao_contem_resultado_de_compatibilidade()
    {
        var sugestoes = CriarGerador().Sugerir(InventarioGpuGargalo(), ModoSugestao.HighEnd);

        Assert.All(sugestoes, s => Assert.NotNull(s.Compatibilidade));
    }
}
