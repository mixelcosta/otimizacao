using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class GeradorGuiaXmpExpoTests
{
    private readonly GeradorGuiaXmpExpo _gerador = new();

    private static IdentificacaoBios Bios(string fabricante) => new()
    {
        FabricanteBruto = fabricante,
        Fabricante = fabricante,
        Modelo = "Test Model",
        ChaveBusca = fabricante.ToLowerInvariant() + "-test-model",
    };

    [Fact]
    public void ASUS_ddr4_usa_docp_e_del()
    {
        var guia = _gerador.Gerar(Bios("ASUS"), "DDR4");

        Assert.Equal("ASUS", guia.Fabricante);
        Assert.Contains("Del", guia.TeclaSetup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(guia.Passos, p => p.Contains("D.O.C.P.", StringComparison.Ordinal));
    }

    [Fact]
    public void ASUS_ddr5_usa_expo()
    {
        var guia = _gerador.Gerar(Bios("ASUS"), "DDR5");

        // Passo 3 deve localizar a opção "EXPO" (não "D.O.C.P." que é para DDR4)
        Assert.Contains(guia.Passos, p => p.Contains("EXPO", StringComparison.Ordinal));
        // Não deve haver passo que diga para configurar "D.O.C.P." como perfil principal
        Assert.DoesNotContain(guia.Passos, p =>
            p.Contains("Localize a opção 'D.O.C.P.'", StringComparison.Ordinal));
    }

    [Fact]
    public void MSI_ddr4_usa_xmp()
    {
        var guia = _gerador.Gerar(Bios("MSI"), "DDR4");

        Assert.Contains(guia.Passos, p => p.Contains("XMP", StringComparison.Ordinal));
        Assert.Contains("Del", guia.TeclaSetup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MSI_ddr5_usa_expo()
    {
        var guia = _gerador.Gerar(Bios("MSI"), "DDR5");

        Assert.Contains(guia.Passos, p => p.Contains("EXPO", StringComparison.Ordinal));
    }

    [Fact]
    public void Gigabyte_ddr4_usa_xmp_ponto()
    {
        var guia = _gerador.Gerar(Bios("Gigabyte"), "DDR4");

        Assert.Contains(guia.Passos, p => p.Contains("X.M.P.", StringComparison.Ordinal));
    }

    [Fact]
    public void Gigabyte_ddr5_usa_expo()
    {
        var guia = _gerador.Gerar(Bios("Gigabyte"), "DDR5");

        Assert.Contains(guia.Passos, p => p.Contains("EXPO", StringComparison.Ordinal));
    }

    [Fact]
    public void ASRock_retorna_exatamente_5_passos()
    {
        var guia = _gerador.Gerar(Bios("ASRock"), "DDR4");

        Assert.Equal(5, guia.Passos.Count);
        Assert.Contains("F2", guia.TeclaSetup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fabricante_desconhecido_retorna_guia_generico_nao_vazio()
    {
        var guia = _gerador.Gerar(Bios("Biostar"), null);

        Assert.NotEmpty(guia.Passos);
        Assert.NotEmpty(guia.Aviso);
        Assert.Equal("DDR4/DDR5", guia.TipoRam);
    }

    [Fact]
    public void Aviso_nao_e_vazio_para_todos_fabricantes()
    {
        foreach (var fab in new[] { "ASUS", "MSI", "Gigabyte", "ASRock", "Desconhecido" })
        {
            var guia = _gerador.Gerar(Bios(fab), "DDR4");
            Assert.NotEmpty(guia.Aviso);
        }
    }
}
