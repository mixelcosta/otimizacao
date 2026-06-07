using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class VersaoBiosTests
{
    [Theory]
    [InlineData("2806", "3405", -1)] // numérico puro
    [InlineData("3405", "2806", 1)]
    [InlineData("2806", "2806", 0)]
    [InlineData("F10", "F12", -1)] // prefixo igual, número maior
    [InlineData("P3.60", "P3.70", -1)] // pontuado
    [InlineData("0805", "805", 0)] // zeros à esquerda
    public void Comparar_respeita_ordem_de_versao(string a, string b, int sinalEsperado)
    {
        var resultado = VersaoBios.Comparar(a, b);
        Assert.Equal(sinalEsperado, Math.Sign(resultado));
    }

    [Fact]
    public void EhMaisRecente_detecta_versao_mais_nova()
    {
        Assert.True(VersaoBios.EhMaisRecente("2806", "3405"));
        Assert.False(VersaoBios.EhMaisRecente("3405", "2806"));
        Assert.False(VersaoBios.EhMaisRecente("2806", "2806"));
    }
}
