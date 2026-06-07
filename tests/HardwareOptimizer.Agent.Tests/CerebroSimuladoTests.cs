using HardwareOptimizer.Cli;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class CerebroSimuladoTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static Inventario Inventario(bool comGpu) => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        Gpu = comGpu ? new[] { new PlacaVideo { Nome = "RTX 3060" } } : Array.Empty<PlacaVideo>(),
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public void Cerebro_so_recomenda_acoes_existentes_no_catalogo()
    {
        var recomendacoes = new CerebroSimulado(Catalogo).Recomendar(Inventario(comGpu: true));

        Assert.NotEmpty(recomendacoes);
        Assert.All(recomendacoes, r => Assert.True(Catalogo.Contem(r.AcaoId!), $"Ação fora do catálogo: {r.AcaoId}"));
    }

    [Fact]
    public void Cerebro_so_sugere_gpu_quando_ha_placa_de_video()
    {
        var comGpu = new CerebroSimulado(Catalogo).Recomendar(Inventario(comGpu: true));
        var semGpu = new CerebroSimulado(Catalogo).Recomendar(Inventario(comGpu: false));

        Assert.Contains(comGpu, r => r.AcaoId == "GPU_HAGS");
        Assert.DoesNotContain(semGpu, r => r.AcaoId == "GPU_HAGS");
    }
}
