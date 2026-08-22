using System.Runtime.Versioning;
using HardwareOptimizer.Agent.EventLog;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-1-5-causa-raiz-event-log.</summary>
public class LeitorEventLogTests
{
    // ── Leitura real — mesmo padrão de LeitorLinux_le_dados_reais_quando_em_linux:
    // gated por OperatingSystem.IsWindows(), roda de verdade só em CI/máquina Windows. ──

    [Fact]
    public async Task LeitorEventLog_LeDadosReaisQuandoEmWindows_NaoLancaERetornaLista()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Teste específico de plataforma.
        }

        var eventos = await new LeitorEventLog().LerAsync(diasRecentes: 30);

        Assert.NotNull(eventos);
        Assert.All(eventos, e => Assert.True(
            e.Tipo is TipoEventoInstabilidade.Bsod or TipoEventoInstabilidade.Whea or TipoEventoInstabilidade.CrashAplicacao));
        Assert.All(eventos, e => Assert.False(string.IsNullOrWhiteSpace(e.Origem)));
    }

    [Fact]
    public async Task LeitorEventLog_DiasRecentesZeroOuNegativo_NaoLanca()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Teste específico de plataforma.
        }

        // Leitura sob demanda nunca deve lançar por parâmetro inválido — mesmo
        // padrão defensivo dos demais leitores (I/O Matrix: "leitura falha").
        var eventosZero = await new LeitorEventLog().LerAsync(diasRecentes: 0);
        var eventosNegativo = await new LeitorEventLog().LerAsync(diasRecentes: -5);

        Assert.NotNull(eventosZero);
        Assert.NotNull(eventosNegativo);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task LeitorEventLog_Cancelado_LancaOperationCanceled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Teste específico de plataforma.
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new LeitorEventLog().LerAsync(diasRecentes: 30, cts.Token));
    }

    // ── ExtrairProcessoOuDriver — lógica pura de parsing, testável sem
    // depender de um EventRecord real do Windows (achado da revisão
    // independente: cobertura zero antes, e regex hardcoded só em inglês). ──

    [Theory]
    [InlineData("Faulting module name: nvwgf2umx.dll, version: 1.0", "nvwgf2umx.dll")]
    [InlineData("Nome do módulo com falha: nvwgf2umx.dll, versão: 1.0", "nvwgf2umx.dll")]
    public void ExtrairProcessoOuDriver_CrashAplicacao_ExtraiModuloEmInglesEPortugues(string mensagem, string esperado)
    {
        var resultado = ExtratorEventoTexto.ExtrairProcessoOuDriver(TipoEventoInstabilidade.CrashAplicacao, mensagem);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("Faulting application name: chrome.exe, version: 1.0", "chrome.exe")]
    [InlineData("Nome do aplicativo com falha: chrome.exe, versão: 1.0", "chrome.exe")]
    public void ExtrairProcessoOuDriver_CrashAplicacao_ExtraiAplicativoQuandoSemModulo(string mensagem, string esperado)
    {
        var resultado = ExtratorEventoTexto.ExtrairProcessoOuDriver(TipoEventoInstabilidade.CrashAplicacao, mensagem);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ExtrairProcessoOuDriver_Bsod_ExtraiNomeDoSys()
    {
        var resultado = ExtratorEventoTexto.ExtrairProcessoOuDriver(
            TipoEventoInstabilidade.Bsod, "O sistema falhou devido a nvlddmkm.sys.");

        Assert.Equal("nvlddmkm.sys", resultado);
    }

    [Fact]
    public void ExtrairProcessoOuDriver_MensagemSemPadraoConhecido_RetornaNull()
    {
        var resultado = ExtratorEventoTexto.ExtrairProcessoOuDriver(
            TipoEventoInstabilidade.CrashAplicacao, "Uma mensagem qualquer sem nenhum dos rótulos esperados.");

        Assert.Null(resultado);
    }

    [Fact]
    public void ExtrairProcessoOuDriver_Whea_SempreRetornaNull()
    {
        var resultado = ExtratorEventoTexto.ExtrairProcessoOuDriver(
            TipoEventoInstabilidade.Whea, "Faulting module name: nao_deveria_extrair.dll");

        Assert.Null(resultado);
    }

    [Fact]
    public void ExtrairProcessoOuDriver_MensagemNulaOuVazia_RetornaNull()
    {
        Assert.Null(ExtratorEventoTexto.ExtrairProcessoOuDriver(TipoEventoInstabilidade.CrashAplicacao, null));
        Assert.Null(ExtratorEventoTexto.ExtrairProcessoOuDriver(TipoEventoInstabilidade.CrashAplicacao, ""));
    }

    // ── Fake ILeitorEventLog — testa delegação/composição sem depender do
    // Windows real (a interface fina permite qualquer consumidor ser testado
    // com dados determinísticos). ────────────────────────────────────────────

    [Fact]
    public async Task FakeLeitorEventLog_DevolveEventosConfigurados_SemTocarWindows()
    {
        var esperado = new List<EventoInstabilidade>
        {
            new()
            {
                Timestamp = DateTimeOffset.UtcNow,
                Tipo = TipoEventoInstabilidade.Whea,
                Origem = "Microsoft-Windows-WHEA-Logger",
                Mensagem = "Erro de hardware simulado.",
            },
        };
        ILeitorEventLog leitor = new LeitorEventLogFake(esperado);

        var eventos = await leitor.LerAsync(diasRecentes: 7);

        Assert.Same(esperado, eventos);
    }

    [Fact]
    public async Task FakeLeitorEventLog_SemEventos_RetornaListaVazia()
    {
        ILeitorEventLog leitor = new LeitorEventLogFake(Array.Empty<EventoInstabilidade>());

        var eventos = await leitor.LerAsync(diasRecentes: 7);

        Assert.Empty(eventos);
    }

    [Fact]
    public async Task FakeLeitorEventLog_RegistraDiasRecentesRecebidos()
    {
        var fake = new LeitorEventLogFake(Array.Empty<EventoInstabilidade>());
        ILeitorEventLog leitor = fake;

        await leitor.LerAsync(diasRecentes: 15);

        Assert.Equal(15, fake.DiasRecentesRecebidos);
    }

    private sealed class LeitorEventLogFake(IReadOnlyList<EventoInstabilidade> eventos) : ILeitorEventLog
    {
        public int? DiasRecentesRecebidos { get; private set; }

        public Task<IReadOnlyList<EventoInstabilidade>> LerAsync(
            int diasRecentes, CancellationToken cancellationToken = default)
        {
            DiasRecentesRecebidos = diasRecentes;
            return Task.FromResult(eventos);
        }
    }
}
