using HardwareOptimizer.Core.Catalog;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class FaixaNumericaTests
{
    [Fact]
    public void Construtor_rejeita_maximo_menor_que_minimo()
    {
        Assert.Throws<ArgumentException>(() => new FaixaNumerica(10, 5));
    }

    [Theory]
    [InlineData(0, 10, 5, true)]
    [InlineData(0, 10, 0, true)] // inclui o mínimo
    [InlineData(0, 10, 10, true)] // inclui o máximo
    [InlineData(0, 10, -1, false)]
    [InlineData(0, 10, 11, false)]
    public void Contem_respeita_intervalo_fechado(double min, double max, double valor, bool esperado)
    {
        Assert.Equal(esperado, new FaixaNumerica(min, max).Contem(valor));
    }

    [Fact]
    public void EstaContidaEm_detecta_continencia()
    {
        var interna = new FaixaNumerica(10, 20);
        var externa = new FaixaNumerica(0, 20);

        Assert.True(interna.EstaContidaEm(externa));
        Assert.False(externa.EstaContidaEm(interna));
    }
}
