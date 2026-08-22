using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

/// <summary>
/// Cobre o overload <see cref="IdentificacaoBios.DeInventario(PlacaMae)"/>
/// adicionado pela spec-1-4 — e a delegação do overload existente
/// <c>DeInventario(Inventario)</c> para ele.
/// </summary>
public sealed class ModelosBiosTests
{
    private static PlacaMae Placa() => new()
    {
        Fabricante = "ASUSTeK Computer Inc.",
        Modelo = "ROG STRIX B550-F",
        VersaoBios = "2806",
        DataBios = "2022-01-10",
        Modo = "UEFI",
        SecureBoot = true,
    };

    private static Inventario Inventario(PlacaMae placa) => new()
    {
        Placa = placa,
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public void DeInventario_ComPlacaMae_NormalizaEMonta()
    {
        var identificacao = IdentificacaoBios.DeInventario(Placa());

        Assert.Equal("ASUSTeK Computer Inc.", identificacao.FabricanteBruto);
        Assert.Equal("ASUS", identificacao.Fabricante);
        Assert.Equal("ROG STRIX B550-F", identificacao.Modelo);
        Assert.Equal("2806", identificacao.VersaoAtual);
        Assert.Equal("2022-01-10", identificacao.Data);
        Assert.Equal("UEFI", identificacao.Modo);
        Assert.True(identificacao.SecureBoot);
        Assert.Equal("asus|rog strix b550-f", identificacao.ChaveBusca);
    }

    [Fact]
    public void DeInventario_ComPlacaMaeNula_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => IdentificacaoBios.DeInventario((PlacaMae)null!));
    }

    [Fact]
    public void DeInventario_ComInventario_DelegaParaOverloadDePlacaMae()
    {
        var placa = Placa();

        var viaInventario = IdentificacaoBios.DeInventario(Inventario(placa));
        var viaPlaca = IdentificacaoBios.DeInventario(placa);

        Assert.Equal(viaPlaca, viaInventario);
    }

    [Fact]
    public void DeInventario_ComInventarioNulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => IdentificacaoBios.DeInventario((Inventario)null!));
    }
}
