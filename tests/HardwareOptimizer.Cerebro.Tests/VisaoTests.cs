using HardwareOptimizer.Cerebro.Visao;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Cerebro.Tests;

public sealed class VisaoTests
{
    private static ImagemEntrada Imagem(string mediaType = "image/png") =>
        new() { Base64 = "ZmFrZQ==", MediaType = mediaType, Descricao = "teste" };

    private static Inventario Inventario(string versaoBios = "2806") => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = versaoBios },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    // ---- LeitorRespostaVisao -------------------------------------------------

    [Fact]
    public void Parser_le_tipo_campos_e_confianca()
    {
        const string json =
            "{\"tipoTela\":\"biosUefi\",\"campos\":{\"versao\":\"3405\",\"fabricante\":\"ASUS\"},"
            + "\"confianca\":\"alta\",\"proximoPasso\":\"confirmar\"}";

        var leitura = new LeitorRespostaVisao().Ler(json, "m");

        Assert.Equal(TipoTela.BiosUefi, leitura.TipoTela);
        Assert.Equal("3405", leitura.Campo("versao"));
        Assert.Equal(NivelConfianca.Alta, leitura.Confianca);
    }

    [Fact]
    public void Parser_default_confianca_baixa_quando_ausente()
    {
        var leitura = new LeitorRespostaVisao().Ler("{\"tipoTela\":\"benchmark\",\"campos\":{}}", "m");
        Assert.Equal(NivelConfianca.Baixa, leitura.Confianca);
    }

    [Fact]
    public void Parser_resposta_invalida_vira_desconhecida_e_pede_nova_foto()
    {
        var leitura = new LeitorRespostaVisao().Ler("isto não é json", "m");

        Assert.Equal(TipoTela.Desconhecida, leitura.TipoTela);
        Assert.Equal(NivelConfianca.Baixa, leitura.Confianca);
        Assert.False(string.IsNullOrWhiteSpace(leitura.ProximoPasso));
    }

    // ---- ModuloVisao ---------------------------------------------------------

    [Fact]
    public async Task Modulo_interpreta_imagem_via_cliente()
    {
        var cliente = new ClienteVisaoFake(
            "{\"tipoTela\":\"biosUefi\",\"campos\":{\"versao\":\"3405\"},\"confianca\":\"alta\",\"proximoPasso\":\"x\"}");

        var leitura = await new ModuloVisao(cliente).InterpretarAsync(Imagem(), CasoUsoVisao.LerVersaoBios);

        Assert.Equal(TipoTela.BiosUefi, leitura.TipoTela);
        Assert.Equal("3405", leitura.Campo("versao"));
        Assert.Equal("fake-visao", leitura.Modelo);
    }

    [Fact]
    public async Task Modulo_rejeita_tipo_de_imagem_nao_suportado()
    {
        var cliente = new ClienteVisaoFake("{}");

        await Assert.ThrowsAsync<NotSupportedException>(
            () => new ModuloVisao(cliente).InterpretarAsync(Imagem("image/bmp"), CasoUsoVisao.Identificar));
    }

    // ---- ConferenciaVisual ---------------------------------------------------

    [Fact]
    public void Conferencia_bios_confere_quando_versao_bate()
    {
        var leitura = new LeituraVisual
        {
            TipoTela = TipoTela.BiosUefi,
            Confianca = NivelConfianca.Alta,
            Campos = new Dictionary<string, string> { ["versao"] = "2806" },
        };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario("2806"));

        Assert.Equal(SituacaoConferencia.Confere, resultado.Situacao);
        Assert.False(resultado.PedirNovaFoto);
    }

    [Fact]
    public void Conferencia_bios_diverge_quando_versao_difere()
    {
        var leitura = new LeituraVisual
        {
            TipoTela = TipoTela.BiosUefi,
            Confianca = NivelConfianca.Alta,
            Campos = new Dictionary<string, string> { ["versao"] = "3405" },
        };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario("2806"));

        Assert.Equal(SituacaoConferencia.Diverge, resultado.Situacao);
    }

    [Fact]
    public void Conferencia_confianca_baixa_pede_nova_foto()
    {
        var leitura = new LeituraVisual { TipoTela = TipoTela.BiosUefi, Confianca = NivelConfianca.Baixa };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario());

        Assert.True(resultado.PedirNovaFoto);
        Assert.Equal(SituacaoConferencia.Inconclusivo, resultado.Situacao);
    }

    [Fact]
    public void Conferencia_etiqueta_confere_com_fabricante_sujo()
    {
        var leitura = new LeituraVisual
        {
            TipoTela = TipoTela.EtiquetaPlaca,
            Confianca = NivelConfianca.Alta,
            Campos = new Dictionary<string, string>
            {
                ["fabricante"] = "ASUSTeK Computer Inc.",
                ["modelo"] = "ROG STRIX B550-F",
            },
        };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario());

        Assert.Equal(SituacaoConferencia.Confere, resultado.Situacao);
    }

    // ---- ConstrutorPromptVisao -----------------------------------------------

    [Fact]
    public void Prompt_sistema_exige_json_com_confianca()
    {
        var sistema = new ConstrutorPromptVisao().MontarSistema();
        Assert.Contains("confianca", sistema, StringComparison.Ordinal);
        Assert.Contains("tipoTela", sistema, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_usuario_foca_no_caso_de_uso()
    {
        var prompt = new ConstrutorPromptVisao().MontarUsuario(CasoUsoVisao.LerVersaoBios);
        Assert.Contains("BIOS", prompt, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ClienteVisaoFake : IClienteVisao
    {
        private readonly string _resposta;

        public ClienteVisaoFake(string resposta) => _resposta = resposta;

        public string Modelo => "fake-visao";

        public Task<string> AnalisarAsync(
            ImagemEntrada imagem, string promptSistema, string promptUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(_resposta);
    }
}
