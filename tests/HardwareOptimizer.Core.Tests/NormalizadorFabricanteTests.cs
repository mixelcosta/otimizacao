using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class NormalizadorFabricanteTests
{
    [Theory]
    [InlineData("ASUSTeK Computer Inc.", "ASUS")]
    [InlineData("ASUS", "ASUS")]
    [InlineData("Micro-Star International Co., Ltd.", "MSI")]
    [InlineData("Gigabyte Technology Co., Ltd.", "Gigabyte")]
    [InlineData("ASRock", "ASRock")]
    [InlineData("Hewlett-Packard", "HP")]
    public void Normalizar_padroniza_nomes_sujos(string bruto, string esperado)
    {
        Assert.Equal(esperado, NormalizadorFabricante.Normalizar(bruto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_trata_vazio(string? bruto)
    {
        Assert.Equal("Desconhecido", NormalizadorFabricante.Normalizar(bruto));
    }

    [Fact]
    public void GerarChaveBusca_normaliza_fabricante_e_colapsa_espacos()
    {
        var chave = NormalizadorFabricante.GerarChaveBusca("ASUSTeK Computer Inc.", "  ROG  STRIX   B550-F  ");
        Assert.Equal("asus|rog strix b550-f", chave);
    }
}
