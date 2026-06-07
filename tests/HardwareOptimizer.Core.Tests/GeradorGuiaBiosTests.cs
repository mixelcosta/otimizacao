using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class GeradorGuiaBiosTests
{
    private static IdentificacaoBios Identificacao(string fabricante) => new()
    {
        FabricanteBruto = fabricante,
        Fabricante = fabricante,
        Modelo = "Placa X",
        ChaveBusca = $"{fabricante.ToLowerInvariant()}|placa x",
    };

    [Theory]
    [InlineData("ASUS", "EZ Flash")]
    [InlineData("MSI", "M-Flash")]
    [InlineData("Gigabyte", "Q-Flash")]
    [InlineData("ASRock", "Instant Flash")]
    public void Guia_usa_o_utilitario_do_fabricante(string fabricante, string utilitarioEsperado)
    {
        var guia = new GeradorGuiaBios().Gerar(Identificacao(fabricante));

        Assert.Contains(utilitarioEsperado, guia.Utilitario, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guia_sempre_traz_avisos_de_seguranca_e_ajustes()
    {
        var guia = new GeradorGuiaBios().Gerar(Identificacao("ASUS"));

        Assert.NotEmpty(guia.Passos);
        Assert.NotEmpty(guia.Avisos);
        Assert.NotEmpty(guia.AjustesRecomendados);
        Assert.Contains(guia.Avisos, a => a.Contains("brick", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fabricante_desconhecido_gera_guia_generico()
    {
        var guia = new GeradorGuiaBios().Gerar(Identificacao("FabricanteX"));

        Assert.NotEmpty(guia.Utilitario);
        Assert.NotEmpty(guia.TeclaSetup);
    }
}
