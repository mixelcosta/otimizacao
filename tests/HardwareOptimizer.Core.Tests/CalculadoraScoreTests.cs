using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Reporting;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class CalculadoraScoreTests
{
    private static Inventario InventarioBom() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806", Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6, Threads = 12, TempIdleC = 38 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 }, new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060", TempIdleC = 41, VersaoDriver = "551.23" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    private static Inventario InventarioFraco() => new()
    {
        Placa = new PlacaMae { Fabricante = "?", Modelo = "?", Modo = "Legacy", SecureBoot = false },
        Cpu = new Processador { Nome = "CPU", Nucleos = 2 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 4 } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    private static readonly IReadOnlyList<ResultadoValidacao> SemTestes = Array.Empty<ResultadoValidacao>();

    private static HashSet<Dominio> Nenhum() => new();

    [Fact]
    public void Todas_as_notas_ficam_entre_0_e_100()
    {
        var calc = new CalculadoraScore();

        foreach (var inv in new[] { InventarioBom(), InventarioFraco() })
        {
            var resultado = calc.Calcular(inv, SemTestes, Nenhum());
            Assert.All(resultado.Scores, s => Assert.InRange(s.Valor, 0, 100));
            Assert.InRange(resultado.NotaFinal, 0, 100);
        }
    }

    [Fact]
    public void Os_sete_dominios_sao_pontuados()
    {
        var resultado = new CalculadoraScore().Calcular(InventarioBom(), SemTestes, Nenhum());

        Assert.Equal(7, resultado.Scores.Count);
        foreach (var dominio in Enum.GetValues<Dominio>())
        {
            Assert.NotNull(resultado.Obter(dominio));
        }
    }

    [Fact]
    public void Bios_uefi_com_secureboot_pontua_mais_que_legacy()
    {
        var calc = new CalculadoraScore();
        var bom = calc.Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Bios)!;
        var fraco = calc.Calcular(InventarioFraco(), SemTestes, Nenhum()).Obter(Dominio.Bios)!;

        Assert.True(bom.Valor > fraco.Valor, $"{bom.Valor} deveria ser > {fraco.Valor}");
    }

    [Fact]
    public void Ram_maior_e_mais_rapida_pontua_mais()
    {
        var calc = new CalculadoraScore();
        var bom = calc.Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Ram)!;
        var fraco = calc.Calcular(InventarioFraco(), SemTestes, Nenhum()).Obter(Dominio.Ram)!;

        Assert.True(bom.Valor > fraco.Valor);
    }

    [Fact]
    public void Estabilidade_sem_testes_eh_neutra()
    {
        var score = new CalculadoraScore().Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Estabilidade)!;
        Assert.Equal(70, score.Valor);
    }

    [Fact]
    public void Estabilidade_com_regressao_despenca()
    {
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Cpu", Ferramenta = "x", Regressao = true, Estabilidade = "Reprovado" },
        };

        var score = new CalculadoraScore().Calcular(InventarioBom(), validacoes, Nenhum()).Obter(Dominio.Estabilidade)!;
        Assert.Equal(30, score.Valor);
    }

    [Fact]
    public void Estabilidade_totalmente_validada_eh_maxima()
    {
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Cpu", Ferramenta = "x", Regressao = false, Estabilidade = "Totalmente validado" },
        };

        var score = new CalculadoraScore().Calcular(InventarioBom(), validacoes, Nenhum()).Obter(Dominio.Estabilidade)!;
        Assert.Equal(100, score.Valor);
    }

    [Fact]
    public void Otimizacoes_de_windows_aplicadas_elevam_a_nota_do_dominio()
    {
        var calc = new CalculadoraScore();
        var sem = calc.Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Windows)!;
        var com = calc.Calcular(InventarioBom(), SemTestes, new HashSet<Dominio> { Dominio.Windows }).Obter(Dominio.Windows)!;

        Assert.True(com.Valor > sem.Valor);
    }

    [Fact]
    public void Hardware_eh_a_media_dos_componentes()
    {
        var resultado = new CalculadoraScore().Calcular(InventarioBom(), SemTestes, Nenhum());
        var esperado = (int)Math.Round(
            (resultado.Obter(Dominio.Cpu)!.Valor
            + resultado.Obter(Dominio.Gpu)!.Valor
            + resultado.Obter(Dominio.Ram)!.Valor
            + resultado.Obter(Dominio.Bios)!.Valor) / 4.0);

        Assert.Equal(esperado, resultado.Obter(Dominio.Hardware)!.Valor);
    }
}
