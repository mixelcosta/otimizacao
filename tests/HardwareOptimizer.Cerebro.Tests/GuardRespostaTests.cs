using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using Xunit;

namespace HardwareOptimizer.Cerebro.Tests;

public sealed class GuardRespostaTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static MatrizDecisao Ler(string resposta) =>
        new LeitorRespostaCerebro().Ler(resposta, Catalogo, OrigemDecisao.Nuvem, "modelo-teste");

    [Fact]
    public void Aceita_acoes_validas_do_catalogo()
    {
        const string json = """
            {"acoes":[
              {"id":"PWR_PLANO_ALTO_DESEMPENHO","prioridade":1,"justificativa":"energia"},
              {"id":"SO_SYSTEM_RESPONSIVENESS","prioridade":2,"parametros":{"percentual_reserva":"20"}}
            ]}
            """;

        var matriz = Ler(json);

        Assert.Equal(2, matriz.Itens.Count);
        Assert.Contains(matriz.Itens, i => i.AcaoId == "PWR_PLANO_ALTO_DESEMPENHO");
        Assert.Equal("20", matriz.Itens.Single(i => i.AcaoId == "SO_SYSTEM_RESPONSIVENESS").Parametros["percentual_reserva"]);
    }

    [Fact]
    public void Descarta_acao_fora_do_catalogo()
    {
        const string json = """
            {"acoes":[
              {"id":"ACAO_INVENTADA","prioridade":1,"justificativa":"alucinação"},
              {"id":"PWR_PLANO_ALTO_DESEMPENHO","prioridade":2}
            ]}
            """;

        var matriz = Ler(json);

        Assert.Single(matriz.Itens);
        Assert.Equal("PWR_PLANO_ALTO_DESEMPENHO", matriz.Itens[0].AcaoId);
        Assert.Contains(matriz.Avisos, a => a.Contains("ACAO_INVENTADA", StringComparison.Ordinal));
    }

    [Fact]
    public void Forca_parametro_acima_do_limite_para_o_padrao_seguro()
    {
        // 25 ultrapassa o limite absoluto (20) -> guard usa o padrão seguro (20).
        const string json = """
            {"acoes":[{"id":"SO_SYSTEM_RESPONSIVENESS","prioridade":1,"parametros":{"percentual_reserva":"25"}}]}
            """;

        var matriz = Ler(json);

        Assert.Equal("20", matriz.Itens.Single().Parametros["percentual_reserva"]);
        Assert.Contains(matriz.Avisos, a => a.Contains("padrão seguro", StringComparison.Ordinal));
    }

    [Fact]
    public void Forca_parametro_fora_da_faixa_segura_para_o_padrao()
    {
        // 5 está dentro da permitida mas fora da segura -> no perfil seguro, vira o padrão (20).
        const string json = """
            {"acoes":[{"id":"SO_SYSTEM_RESPONSIVENESS","prioridade":1,"parametros":{"percentual_reserva":"5"}}]}
            """;

        var matriz = Ler(json);

        Assert.Equal("20", matriz.Itens.Single().Parametros["percentual_reserva"]);
    }

    [Fact]
    public void Json_malformado_gera_matriz_vazia_sem_lancar()
    {
        var matriz = Ler("isto não é json");

        Assert.Empty(matriz.Itens);
        Assert.NotEmpty(matriz.Avisos);
    }

    [Fact]
    public void Tolera_cercas_de_markdown()
    {
        const string resposta = "```json\n{\"acoes\":[{\"id\":\"PWR_PLANO_ALTO_DESEMPENHO\",\"prioridade\":1}]}\n```";

        var matriz = Ler(resposta);

        Assert.Single(matriz.Itens);
    }

    [Fact]
    public void Renumera_prioridade_por_ordem()
    {
        const string json = """
            {"acoes":[
              {"id":"NET_THROTTLING_DESABILITAR","prioridade":9},
              {"id":"PWR_PLANO_ALTO_DESEMPENHO","prioridade":3}
            ]}
            """;

        var matriz = Ler(json);

        Assert.Equal("PWR_PLANO_ALTO_DESEMPENHO", matriz.Itens[0].AcaoId);
        Assert.Equal(1, matriz.Itens[0].Prioridade);
        Assert.Equal(2, matriz.Itens[1].Prioridade);
    }
}
