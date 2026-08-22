using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.Tests;

/// <summary>Cobre o fluxo de UI da spec-2-1-deteccao-pasta-termica-ressecada.</summary>
public class DiagnosticoManutencaoViewModelTests
{
    // ── Estado inicial ─────────────────────────────────────────────────────────

    [Fact]
    public void EstadoInicial_SemAchado_NaoDiagnosticando()
    {
        var vm = new DiagnosticoManutencaoViewModel(new AgenteManutencaoFake(achado: null));

        Assert.False(vm.TemAchado);
        Assert.Null(vm.Achado);
        Assert.False(vm.Diagnosticando);
        Assert.False(vm.JaDiagnosticou);
        Assert.Equal("—", vm.TemperaturaCargaTexto);
    }

    // ── TemperaturaCargaTexto ─────────────────────────────────────────────────

    [Fact]
    public async Task TemperaturaCargaTexto_ComLeituraRealSobCarga_FormataComUmaCasaDecimal()
    {
        var achado = new AchadoManutencao
        {
            TemperaturaIdleC = 60.0,
            TemperaturaCargaC = 72.3,
            Custo = new Custo { ValorEstimado = 50m },
            Justificativa = "Temperatura em repouso acima do limiar.",
        };
        var vm = new DiagnosticoManutencaoViewModel(new AgenteManutencaoFake(achado));

        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.Equal("72.3 °C", vm.TemperaturaCargaTexto);
    }

    /// <summary>
    /// Corrigido na revisão independente: sem leitura real sob carga
    /// (<c>TemperaturaCargaC</c> nulo), a tela mostra "—" — nunca um valor
    /// fabricado como se fosse uma medição real.
    /// </summary>
    [Fact]
    public async Task TemperaturaCargaTexto_SemLeituraRealSobCarga_MostraTraco()
    {
        var achado = new AchadoManutencao
        {
            TemperaturaIdleC = 60.0,
            TemperaturaCargaC = null,
            Custo = new Custo { ValorEstimado = 50m },
            Justificativa = "Temperatura em repouso acima do limiar.",
        };
        var vm = new DiagnosticoManutencaoViewModel(new AgenteManutencaoFake(achado));

        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.True(vm.TemAchado);
        Assert.Equal("—", vm.TemperaturaCargaTexto);
    }

    // ── DiagnosticarCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task DiagnosticarAsync_SemAgente_NaoLancaExcecaoNemAlteraEstado()
    {
        var vm = new DiagnosticoManutencaoViewModel(); // sem agente

        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.False(vm.JaDiagnosticou);
        Assert.False(vm.Diagnosticando);
        Assert.Null(vm.Achado);
    }

    [Fact]
    public async Task DiagnosticarAsync_BackendSinalizaAchado_PopulaAchado()
    {
        var achado = new AchadoManutencao
        {
            TemperaturaIdleC = 60.0,
            TemperaturaCargaC = 72.0,
            Custo = new Custo { ValorEstimado = 50m },
            Justificativa = "Temperatura em repouso acima do limiar.",
        };
        var agente = new AgenteManutencaoFake(achado);
        var vm = new DiagnosticoManutencaoViewModel(agente);

        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.True(vm.TemAchado);
        Assert.Equal(60.0, vm.Achado!.TemperaturaIdleC);
        Assert.Equal(72.0, vm.Achado.TemperaturaCargaC);
        Assert.True(vm.JaDiagnosticou);
        Assert.False(vm.Diagnosticando);
        Assert.Equal(1, agente.Chamadas);
        Assert.Equal("diagnosticarmanutencao", agente.UltimoMetodo);
    }

    /// <summary>
    /// Guard anti-alucinação (Boundaries §Always da spec-2-1): sem sinal real
    /// do backend, a tela nunca inventa/mostra um achado.
    /// </summary>
    [Fact]
    public async Task DiagnosticarAsync_BackendSemSinal_NaoMostraAchado()
    {
        var agente = new AgenteManutencaoFake(achado: null);
        var vm = new DiagnosticoManutencaoViewModel(agente);

        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.False(vm.TemAchado);
        Assert.Null(vm.Achado);
        Assert.True(vm.JaDiagnosticou);
    }

    [Fact]
    public async Task DiagnosticarAsync_FalhaNoAgente_MostraMensagemDeFalha()
    {
        var agente = new AgenteManutencaoFake(sucesso: false);
        var vm = new DiagnosticoManutencaoViewModel(agente);

        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.False(vm.TemAchado);
        Assert.False(vm.Diagnosticando);
        Assert.Contains("Falha", vm.StatusText);
    }

    [Fact]
    public async Task DiagnosticarAsync_SegundaChamadaSemAchado_LimpaAchadoAnterior()
    {
        var achado = new AchadoManutencao
        {
            TemperaturaIdleC = 60.0,
            TemperaturaCargaC = 72.0,
            Custo = new Custo { ValorEstimado = 50m },
            Justificativa = "Temperatura em repouso acima do limiar.",
        };
        var agente = new AgenteManutencaoFake(achado);
        var vm = new DiagnosticoManutencaoViewModel(agente);

        await vm.DiagnosticarCommand.ExecuteAsync(null);
        Assert.True(vm.TemAchado);

        agente.ProximoAchado = null;
        await vm.DiagnosticarCommand.ExecuteAsync(null);

        Assert.False(vm.TemAchado);
        Assert.Null(vm.Achado);
    }

    // ── Fake ──────────────────────────────────────────────────────────────────

    private sealed class AgenteManutencaoFake : IRoteadorIpc
    {
        private readonly bool _sucesso;
        public AchadoManutencao? ProximoAchado { get; set; }
        public int Chamadas { get; private set; }
        public string? UltimoMetodo { get; private set; }

        public AgenteManutencaoFake(AchadoManutencao? achado = null, bool sucesso = true)
        {
            ProximoAchado = achado;
            _sucesso = sucesso;
        }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            Chamadas++;
            UltimoMetodo = req.Metodo;
            var resp = _sucesso
                ? RespostaIpc.Ok(req.Id, ProximoAchado)
                : RespostaIpc.Falha(req.Id, "Erro simulado.");
            return Task.FromResult(resp);
        }
    }
}
