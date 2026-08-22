using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-1-5-causa-raiz-event-log.</summary>
public class CorrelacionadorCausaRaizTests
{
    private static CorrelacionadorCausaRaiz Criar() => new();

    private static EventoInstabilidade Evento(
        TipoEventoInstabilidade tipo,
        string origem = "Microsoft-Windows-WER-SystemErrorReporting",
        string? processoOuDriver = null,
        string? mensagem = null) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Tipo = tipo,
        Origem = origem,
        ProcessoOuDriver = processoOuDriver,
        Mensagem = mensagem,
    };

    private static InfoDriver Driver(string fabricante, string descricao = "GeForce RTX 3060") => new()
    {
        HardwareId = "PCI\\VEN_10DE&DEV_2504",
        Descricao = descricao,
        Fabricante = fabricante,
        Status = StatusDriver.AtualizacaoDisponivel,
    };

    private static InfoBios Bios() => new()
    {
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoAtual = "2806",
        VersaoDisponivel = "3405",
        TeclaSetup = "Del",
        Utilitario = "EZ Flash 3",
    };

    // ── Regra 1: correspondência de fabricante ──────────────────────────────

    [Fact]
    public void Correlacionar_EventoCitaFabricanteDeDriverDesatualizado_AtribuiCausaComDescricaoDoDriver()
    {
        var evento = Evento(TipoEventoInstabilidade.Whea, mensagem: "Erro reportado pelo driver NVIDIA no barramento PCIe.");
        var driver = Driver("NVIDIA");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Single(resultado);
        Assert.Equal(driver.Descricao, resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_FabricanteNoProcessoOuDriver_AtribuiCausa()
    {
        var evento = Evento(TipoEventoInstabilidade.CrashAplicacao, processoOuDriver: "RealtekHD.sys");
        var driver = Driver("Realtek", descricao: "Realtek High Definition Audio");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Equal("Realtek High Definition Audio", resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_FabricanteNaOrigem_AtribuiCausa()
    {
        var evento = Evento(TipoEventoInstabilidade.Bsod, origem: "NVIDIA Display Driver");
        var driver = Driver("NVIDIA");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Equal(driver.Descricao, resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_CorrespondenciaFabricante_EhCaseInsensitive()
    {
        var evento = Evento(TipoEventoInstabilidade.Whea, mensagem: "Falha reportada por nvidia no chipset.");
        var driver = Driver("NVIDIA");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Equal(driver.Descricao, resultado[0].CausaProvavel);
    }

    // ── Regra 2: heurística WHEA↔BIOS ───────────────────────────────────────

    [Fact]
    public void Correlacionar_EventoWheaComBiosDesatualizadaSinalizada_AtribuiCausaBios()
    {
        var evento = Evento(TipoEventoInstabilidade.Whea, mensagem: "Erro de hardware não corrigível na memória.");

        var resultado = Criar().Correlacionar([evento], driversDesatualizados: [], bios: Bios());

        Assert.Equal("BIOS desatualizada", resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_EventoWheaSemBiosDesatualizada_NaoAtribuiCausa()
    {
        var evento = Evento(TipoEventoInstabilidade.Whea, mensagem: "Erro de hardware não corrigível.");

        var resultado = Criar().Correlacionar([evento], driversDesatualizados: [], bios: null);

        Assert.Null(resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_EventoNaoWheaComBiosDesatualizada_NaoAtribuiCausaBios()
    {
        // A heurística WHEA↔BIOS só se aplica a eventos WHEA — BSOD/crash de
        // aplicação não são cobertos por essa regra, mesmo com BIOS desatualizada.
        var evento = Evento(TipoEventoInstabilidade.Bsod, mensagem: "Bugcheck 0x0000007E.");

        var resultado = Criar().Correlacionar([evento], driversDesatualizados: [], bios: Bios());

        Assert.Null(resultado[0].CausaProvavel);
    }

    // ── Sem correlação plausível (guard anti-alucinação) ────────────────────

    [Fact]
    public void Correlacionar_SemDriverOuBiosCorrelacionavel_NaoAtribuiCausa()
    {
        var evento = Evento(TipoEventoInstabilidade.CrashAplicacao, mensagem: "Faulting module name: unknown.dll");
        var driver = Driver("Intel"); // fabricante não citado no evento

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Null(resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_ListaVaziaDeEventos_RetornaListaVazia()
    {
        var resultado = Criar().Correlacionar([], [Driver("NVIDIA")], Bios());

        Assert.Empty(resultado);
    }

    [Fact]
    public void Correlacionar_PreservaCamposOriginaisDoEvento()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evento = new EventoInstabilidade
        {
            Timestamp = timestamp,
            Tipo = TipoEventoInstabilidade.Bsod,
            Origem = "Microsoft-Windows-WER-SystemErrorReporting",
            ProcessoOuDriver = "nvlddmkm.sys",
            Mensagem = "Bugcheck causado por nvlddmkm.sys",
        };
        var driver = Driver("NVIDIA");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Equal(timestamp, resultado[0].Timestamp);
        Assert.Equal(TipoEventoInstabilidade.Bsod, resultado[0].Tipo);
        Assert.Equal("nvlddmkm.sys", resultado[0].ProcessoOuDriver);
    }

    [Fact]
    public void Correlacionar_DriverComFabricanteMicrosoft_NaoGeraFalsoPositivoEmEventoWhea()
    {
        // "Microsoft" é um valor real de InfoDriver.Fabricante pra drivers
        // built-in/genéricos, mas também aparece como substring em praticamente
        // todo evento WHEA (Origem = "Microsoft-Windows-WHEA-Logger") — sem essa
        // exclusão, isso violaria o guard anti-alucinação (achado da revisão).
        var evento = Evento(TipoEventoInstabilidade.Whea, origem: "Microsoft-Windows-WHEA-Logger");
        var driver = Driver("Microsoft", descricao: "Standard NVMe Express Controller");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Null(resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_DriverComFabricanteGenericoDemais_IgnoradoMesmoComMencaoExplicita()
    {
        var evento = Evento(TipoEventoInstabilidade.CrashAplicacao, mensagem: "Falha reportada pelo driver Standard no sistema.");
        var driver = Driver("Standard");

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Null(resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_DriverSemFabricante_IgnoradoNaCorrespondencia()
    {
        var evento = Evento(TipoEventoInstabilidade.Bsod, mensagem: "Falha genérica de driver.");
        var driver = new InfoDriver
        {
            HardwareId = "PCI\\VEN_0000",
            Descricao = "Dispositivo Desconhecido",
            Fabricante = null,
            Status = StatusDriver.AtualizacaoDisponivel,
        };

        var resultado = Criar().Correlacionar([evento], [driver], bios: null);

        Assert.Null(resultado[0].CausaProvavel);
    }

    [Fact]
    public void Correlacionar_EventosNulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Criar().Correlacionar(null!, [], null));
    }

    [Fact]
    public void Correlacionar_DriversNulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Criar().Correlacionar([], null!, null));
    }
}
