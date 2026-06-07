using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class AnalisadorBiosTests
{
    private static IdentificacaoBios Identificacao(string versaoAtual) => new()
    {
        FabricanteBruto = "ASUSTeK",
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoAtual = versaoAtual,
        ChaveBusca = "asus|rog strix b550-f",
    };

    private static InfoBiosFabricante Info(string versao, GanhoEstimado ganho) => new()
    {
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoMaisRecente = versao,
        Fonte = "https://www.asus.com/support/",
        Ganho = ganho,
        Motivo = "Estabilidade de memória.",
    };

    [Fact]
    public void Sem_info_do_fabricante_nao_recomenda_e_risco_medio()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("2806"), info: null);

        Assert.False(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Medio, decisao.Risco);
    }

    [Fact]
    public void Versao_atual_igual_ou_superior_nao_recomenda()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("3405"), Info("3405", GanhoEstimado.Medio));

        Assert.False(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Nenhum, decisao.Risco);
    }

    [Fact]
    public void Versao_mais_nova_sem_ganho_real_nao_recomenda()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("2806"), Info("3405", GanhoEstimado.Nenhum));

        Assert.False(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Medio, decisao.Risco);
    }

    [Fact]
    public void Versao_mais_nova_com_ganho_recomenda_com_risco_medio()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("2806"), Info("3405", GanhoEstimado.Medio));

        Assert.True(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Medio, decisao.Risco); // flash de BIOS nunca é risco baixo
        Assert.Equal("3405", decisao.VersaoRecomendada);
        Assert.Equal("2806", decisao.VersaoAtual);
    }
}
