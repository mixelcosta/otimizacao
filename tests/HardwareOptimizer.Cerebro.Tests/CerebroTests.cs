using System.Text.Json;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Cerebro.Tests;

public sealed class CerebroTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private const string Usuario = "michel-secreto";

    private static Inventario Sanitizado(bool comGpu = true) => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        Gpu = comGpu ? new[] { new PlacaVideo { Nome = "RTX 3060" } } : Array.Empty<PlacaVideo>(),
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
        Identificadores = null, // sanitizado
    };

    private static Inventario ComPii() => Sanitizado() with
    {
        Identificadores = new IdentificadoresSensiveis { NomeUsuario = Usuario },
    };

    // ---- CerebroLocal --------------------------------------------------------

    [Fact]
    public async Task Local_propoe_apenas_acoes_do_catalogo_de_baixo_risco()
    {
        var matriz = await new CerebroLocal().ProporAsync(Sanitizado(), Catalogo);

        Assert.NotEmpty(matriz.Itens);
        Assert.Equal(OrigemDecisao.Local, matriz.Origem);
        Assert.All(matriz.Itens, i => Assert.True(Catalogo.Contem(i.AcaoId)));
        Assert.All(matriz.Itens, i => Assert.True(i.Risco <= NivelRisco.Baixo));
    }

    [Fact]
    public async Task Local_so_propoe_gpu_quando_ha_placa_de_video()
    {
        var com = await new CerebroLocal().ProporAsync(Sanitizado(comGpu: true), Catalogo);
        var sem = await new CerebroLocal().ProporAsync(Sanitizado(comGpu: false), Catalogo);

        Assert.Contains(com.Itens, i => i.AcaoId == "GPU_HAGS");
        Assert.DoesNotContain(sem.Itens, i => i.AcaoId == "GPU_HAGS");
    }

    [Fact]
    public async Task Matriz_serializa_para_json_valido()
    {
        var matriz = await new CerebroLocal().ProporAsync(Sanitizado(), Catalogo);

        var json = JsonSerializer.Serialize(matriz);
        using var doc = JsonDocument.Parse(json); // não lança => JSON válido

        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // ---- CerebroLlm ----------------------------------------------------------

    [Fact]
    public async Task Llm_constroi_matriz_a_partir_da_resposta()
    {
        var cliente = new ClienteFake(
            "{\"acoes\":[{\"id\":\"PWR_PLANO_ALTO_DESEMPENHO\",\"prioridade\":1,\"justificativa\":\"ok\"}]}");

        var matriz = await new CerebroLlm(cliente).ProporAsync(Sanitizado(), Catalogo);

        Assert.Equal(OrigemDecisao.Nuvem, matriz.Origem);
        Assert.Equal("fake-1", matriz.Modelo);
        Assert.Single(matriz.Itens);
    }

    [Fact]
    public async Task Llm_filtra_acao_alucinada_pelo_guard()
    {
        var cliente = new ClienteFake(
            "{\"acoes\":[{\"id\":\"NAO_EXISTE\",\"prioridade\":1},{\"id\":\"PWR_PLANO_ALTO_DESEMPENHO\",\"prioridade\":2}]}");

        var matriz = await new CerebroLlm(cliente).ProporAsync(Sanitizado(), Catalogo);

        Assert.Single(matriz.Itens);
        Assert.Equal("PWR_PLANO_ALTO_DESEMPENHO", matriz.Itens[0].AcaoId);
    }

    [Fact]
    public async Task Llm_recusa_inventario_com_pii()
    {
        var cliente = new ClienteFake("{\"acoes\":[]}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CerebroLlm(cliente).ProporAsync(ComPii(), Catalogo));
    }

    // ---- ConstrutorPrompt ----------------------------------------------------

    [Fact]
    public void Prompt_sistema_fixa_a_regra_do_catalogo_fechado()
    {
        var sistema = new ConstrutorPrompt().MontarSistema(Catalogo);

        Assert.Contains("APENAS", sistema, StringComparison.Ordinal);
        Assert.Contains("acoes", sistema, StringComparison.Ordinal); // formato JSON exigido
    }

    [Fact]
    public void Prompt_usuario_lista_ids_do_catalogo_e_nao_vaza_segredo()
    {
        var prompt = new ConstrutorPrompt().MontarUsuario(Sanitizado(), Catalogo);

        Assert.Contains("PWR_PLANO_ALTO_DESEMPENHO", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(Usuario, prompt, StringComparison.Ordinal); // inventário sanitizado
    }

    private sealed class ClienteFake : IClienteLlm
    {
        private readonly string _resposta;

        public ClienteFake(string resposta) => _resposta = resposta;

        public string Modelo => "fake-1";

        public Task<string> ResponderAsync(
            string promptSistema, string promptUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(_resposta);

        public Task<string> ResponderConversaAsync(
            string promptSistema,
            IReadOnlyList<(string Role, string Conteudo)> historico,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_resposta);
    }
}
