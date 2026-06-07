using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Reporting;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class GeradorRelatorioTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806", Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6, TempIdleC = 38 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    [Fact]
    public void Relatorio_consolida_notas_alteracoes_e_destaques()
    {
        var alteracoes = new[]
        {
            new AlteracaoResumo("registro:SystemResponsiveness", "20", "10"),
        };
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Windows", Ferramenta = "x", Regressao = false, Estabilidade = "Totalmente validado" },
        };

        var relatorio = new GeradorRelatorio().Gerar(
            Inventario(), validacoes, alteracoes, new HashSet<Dominio> { Dominio.Windows });

        Assert.Equal(7, relatorio.Scores.Count);
        Assert.InRange(relatorio.NotaFinal, 0, 100);
        Assert.False(relatorio.RegressaoDetectada);
        Assert.Single(relatorio.Alteracoes);
        Assert.NotEmpty(relatorio.Destaques);
        Assert.NotEmpty(relatorio.ResumoExecutivo);
    }

    [Fact]
    public void Regressao_eh_refletida_no_relatorio()
    {
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Cpu", Ferramenta = "x", Regressao = true, Estabilidade = "Reprovado" },
        };

        var relatorio = new GeradorRelatorio().Gerar(
            Inventario(), validacoes, Array.Empty<AlteracaoResumo>(), new HashSet<Dominio>());

        Assert.True(relatorio.RegressaoDetectada);
    }
}
