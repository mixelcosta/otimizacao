using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HardwareOptimizer.Features.Upgrade.Tests;

public sealed class ValidadorCompatibilidadeTests
{
    private readonly ValidadorCompatibilidade _validador = new(NullLogger<ValidadorCompatibilidade>.Instance);

    // Usa "Gigabyte B550 AORUS Elite" que está no catálogo (AM4, DDR4)
    private static Inventario InventarioPadrao(string mobo = "Gigabyte B550 AORUS Elite", int ramPentes = 1) => new()
    {
        Placa = new PlacaMae { Fabricante = "Gigabyte", Modelo = mobo, VersaoBios = "1.0" },
        Cpu = new Processador { Nome = "AMD Ryzen 5 5600X", Nucleos = 6 },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060" } },
        Memoria = Enumerable.Range(0, ramPentes)
            .Select(_ => new ModuloMemoria { TamanhoGb = 8, VelocidadeMhz = 3200 })
            .ToList(),
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public void Cpu_com_socket_incompativel_retorna_incompativel()
    {
        // Gigabyte B550 AORUS Elite é AM4; CPU LGA1700 é incompatível
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Cpu,
            Modelo = "Intel Core i9-14900K",
            Socket = "LGA1700",
        };

        var r = _validador.Validar(InventarioPadrao(), nova);

        Assert.False(r.Compativel);
        Assert.NotEmpty(r.Incompatibilidades);
    }

    [Fact]
    public void Cpu_com_socket_am4_em_placa_am4_retorna_compativel()
    {
        // Ryzen 5 5600 usa AM4, Gigabyte B550 AORUS Elite é AM4
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Cpu,
            Modelo = "AMD Ryzen 5 5600",
            Socket = "AM4",
        };

        var r = _validador.Validar(InventarioPadrao(), nova);

        Assert.True(r.Compativel);
        Assert.Empty(r.Incompatibilidades);
    }

    [Fact]
    public void Ram_ddr5_em_placa_ddr4_retorna_incompativel()
    {
        // Gigabyte B550 AORUS Elite é DDR4; tentar instalar DDR5 → incompatível
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Ram,
            Modelo = "Corsair DDR5-5600 16GB",
            TipoDdr = "DDR5",
            VelocidadeMhz = 5600,
        };

        var r = _validador.Validar(InventarioPadrao(), nova);

        Assert.False(r.Compativel);
        Assert.Contains(r.Incompatibilidades, i => i.Contains("DDR5") || i.Contains("DDR4"));
    }

    [Fact]
    public void Ram_ddr4_em_placa_ddr4_retorna_compativel()
    {
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Ram,
            Modelo = "Kingston DDR4-3200 16GB",
            TipoDdr = "DDR4",
            VelocidadeMhz = 3200,
        };

        var r = _validador.Validar(InventarioPadrao(), nova);

        Assert.True(r.Compativel);
    }

    [Fact]
    public void Gpu_com_tdp_alto_gera_aviso_de_fonte()
    {
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Gpu,
            Modelo = "NVIDIA GeForce RTX 4090",
            TdpW = 450,
        };

        var r = _validador.Validar(InventarioPadrao(), nova);

        Assert.True(r.Compativel);
        Assert.NotEmpty(r.Avisos);
    }

    [Fact]
    public void Ram_single_pente_sugere_dual_channel()
    {
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Ram,
            Modelo = "Kingston DDR4-3200 8GB",
            TipoDdr = "DDR4",
        };

        var r = _validador.Validar(InventarioPadrao(ramPentes: 1), nova);

        Assert.NotNull(r.GanhoEstimadoPercent);
        Assert.True(r.GanhoEstimadoPercent > 0);
    }

    [Fact]
    public void Ram_dual_channel_nao_gera_sugestao_adicional()
    {
        var nova = new PecaSubstituta
        {
            Tipo = TipoPecaUpgrade.Ram,
            Modelo = "Kingston DDR4-3200 16GB",
            TipoDdr = "DDR4",
        };

        // 2 pentes já = Dual-Channel; não deve sugerir adicionar mais
        var r = _validador.Validar(InventarioPadrao(ramPentes: 2), nova);

        Assert.Null(r.GanhoEstimadoPercent);
    }
}
