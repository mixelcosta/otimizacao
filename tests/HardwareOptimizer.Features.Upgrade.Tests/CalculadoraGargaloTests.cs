using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Upgrade;
using Xunit;

namespace HardwareOptimizer.Features.Upgrade.Tests;

public sealed class CalculadoraGargaloTests
{
    private readonly CalculadoraGargalo _calc = new();

    private static Inventario Com(string cpu, string gpu = "") => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = cpu },
        Gpu = string.IsNullOrEmpty(gpu)
            ? Array.Empty<PlacaVideo>()
            : new[] { new PlacaVideo { Nome = gpu } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public void Cpu_fraca_vs_Gpu_forte_identifica_cpu_como_gargalo()
    {
        // R5 3600 score=1100 vs RTX 4090 score=3000 → ratio=0.367 < 0.7
        var r = _calc.Calcular(Com("AMD Ryzen 5 3600", "RTX 4090"));

        Assert.Equal("CPU", r.ComponenteLimitante);
        Assert.True(r.PorcentagemGargalo > 0);
        Assert.True(r.GanhoEstimadoPercent > 0);
    }

    [Fact]
    public void Cpu_forte_vs_Gpu_fraca_identifica_gpu_como_gargalo()
    {
        // i9-14900K score=2800 vs RX 6600 XT score=1200 → ratio=2.33 > 1.4
        var r = _calc.Calcular(Com("Intel Core i9-14900K", "AMD Radeon RX 6600 XT"));

        Assert.Equal("GPU", r.ComponenteLimitante);
        Assert.True(r.PorcentagemGargalo > 0);
    }

    [Fact]
    public void Setup_equilibrado_retorna_balanceado()
    {
        // Ryzen 5 5600X score=1500 vs RTX 3060 Ti score=1500 → ratio=1.0
        var r = _calc.Calcular(Com("AMD Ryzen 5 5600X", "NVIDIA GeForce RTX 3060 Ti"));

        Assert.Equal("Balanceado", r.ComponenteLimitante);
        Assert.Equal(0, r.PorcentagemGargalo);
        Assert.Equal(0, r.GanhoEstimadoPercent);
    }

    [Fact]
    public void Componentes_desconhecidos_retorna_descricao_sem_gargalo()
    {
        var r = _calc.Calcular(Com("CPU Exótica XYZ", "GPU Desconhecida 9900"));

        Assert.Equal("Desconhecido", r.ComponenteLimitante);
        Assert.Equal(0, r.PorcentagemGargalo);
    }

    [Fact]
    public void Sem_gpu_no_inventario_usa_fallback_e_nao_falha()
    {
        var r = _calc.Calcular(Com("Intel Core i9-14900K"));

        Assert.NotNull(r.ComponenteLimitante);
        Assert.NotNull(r.Descricao);
    }

    [Fact]
    public void Porcentagem_gargalo_cpu_calculada_corretamente()
    {
        // R5 3600 (1100) vs RTX 4090 (3000) → ratio=0.3667 → pct=(1-0.3667)*100=63.3
        var r = _calc.Calcular(Com("AMD Ryzen 5 3600", "NVIDIA GeForce RTX 4090"));

        Assert.True(r.PorcentagemGargalo > 60 && r.PorcentagemGargalo < 70);
    }

    [Fact]
    public void Resultado_contem_descricao_nao_vazia()
    {
        var r = _calc.Calcular(Com("AMD Ryzen 5 5600X", "NVIDIA GeForce RTX 3060 Ti"));

        Assert.NotEmpty(r.Descricao);
    }

    [Fact]
    public void Setup_quase_no_limite_balanceado_nao_e_gargalo()
    {
        // Ryzen 7 5700X score=1800 vs RTX 3070 score=1700 → ratio≈1.06 (entre 0.7 e 1.4)
        var r = _calc.Calcular(Com("AMD Ryzen 7 5700X", "NVIDIA GeForce RTX 3070"));

        Assert.Equal("Balanceado", r.ComponenteLimitante);
    }
}
