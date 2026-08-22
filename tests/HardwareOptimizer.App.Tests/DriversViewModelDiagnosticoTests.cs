using System.Text.Json;
using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-1-5-causa-raiz-event-log (ponta a ponta na ViewModel).</summary>
public class DriversViewModelDiagnosticoTests
{
    private static InfoDriver DriverAtualizado() => new()
    {
        HardwareId = "PCI\\VEN_10DE&DEV_2504",
        Descricao = "GeForce RTX 3060",
        Fabricante = "NVIDIA",
        Status = StatusDriver.Atualizado,
    };

    private static InfoDriver DriverDesatualizado() => new()
    {
        HardwareId = "PCI\\VEN_10DE&DEV_2504",
        Descricao = "GeForce RTX 3060",
        Fabricante = "NVIDIA",
        Status = StatusDriver.AtualizacaoDisponivel,
    };

    private static EventoInstabilidade Evento(string? causaProvavel = null) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Tipo = TipoEventoInstabilidade.Whea,
        Origem = "Microsoft-Windows-WHEA-Logger",
        Mensagem = "Erro de hardware simulado.",
        CausaProvavel = causaProvavel,
    };

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_ComResultado_PopulaEventos()
    {
        var agente = new AgenteDiagnosticoFake([Evento("GeForce RTX 3060")]);
        var vm = new DriversViewModel(agente);

        await vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null);

        Assert.Single(vm.Eventos);
        Assert.Equal("GeForce RTX 3060", vm.Eventos[0].CausaProvavel);
    }

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_ListaVazia_StatusIndicaNenhumEncontrado()
    {
        var agente = new AgenteDiagnosticoFake([]);
        var vm = new DriversViewModel(agente);

        await vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null);

        Assert.Empty(vm.Eventos);
        Assert.Contains("Nenhum", vm.StatusTextDiagnostico);
    }

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_SemAgente_NaoFalha()
    {
        var vm = new DriversViewModel();

        var ex = await Record.ExceptionAsync(() => vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null));

        Assert.Null(ex);
        Assert.Empty(vm.Eventos);
    }

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_Falha_LimpaListaEMostraErro()
    {
        var agente = new AgenteDiagnosticoFalhaFake();
        var vm = new DriversViewModel(agente);

        await vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null);

        Assert.Empty(vm.Eventos);
        Assert.Contains("Falha", vm.StatusTextDiagnostico);
    }

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_SoEnviaDriversComAtualizacaoDisponivel()
    {
        var agente = new AgenteDiagnosticoFake([]);
        var vm = new DriversViewModel(agente);
        vm.Popular([DriverAtualizado(), DriverDesatualizado()]);

        await vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null);

        Assert.Single(agente.DriversRecebidos);
        Assert.Equal(StatusDriver.AtualizacaoDisponivel, agente.DriversRecebidos[0].Status);
    }

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_EnviaBiosJaPopulada()
    {
        var infoBios = new InfoBios
        {
            Fabricante = "ASUS",
            Modelo = "ROG STRIX B550-F",
            VersaoAtual = "2806",
            VersaoDisponivel = "3405",
            TeclaSetup = "Del",
            Utilitario = "EZ Flash 3",
        };
        var agente = new AgenteDiagnosticoFake([], resultadoVerificarBios: infoBios);
        var vm = new DriversViewModel(agente);
        vm.PopularBios(new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "2806" });

        await vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null);

        Assert.NotNull(agente.BiosRecebida);
        Assert.Equal("ASUS", agente.BiosRecebida!.Fabricante);
    }

    [Fact]
    public async Task DiagnosticarCausaRaizAsync_ComEventoSemCausa_NaoInventaCausa()
    {
        var agente = new AgenteDiagnosticoFake([Evento(causaProvavel: null)]);
        var vm = new DriversViewModel(agente);

        await vm.DiagnosticarCausaRaizCommand.ExecuteAsync(null);

        Assert.Single(vm.Eventos);
        Assert.Null(vm.Eventos[0].CausaProvavel);
    }

    [Fact]
    public void Popular_PopularProgramas_PopularBios_NuncaDisparamDiagnosticoAutomaticamente()
    {
        // Boundaries §Never da spec-1-5: leitura do Event Log é sempre sob
        // demanda (botão) — nunca disparada pelo fluxo automático de SCAN
        // (Popular/PopularProgramas/PopularBios, chamados por ShellViewModel).
        var agente = new AgenteContadorDiagnosticoFake();
        var vm = new DriversViewModel(agente);

        vm.Popular([DriverDesatualizado()]);
        vm.PopularProgramas([]);
        vm.PopularBios(new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "2806" });

        Assert.Equal(0, agente.ChamadasDiagnostico);
        Assert.Empty(vm.Eventos);
    }

    private sealed class AgenteContadorDiagnosticoFake : IRoteadorIpc
    {
        public int ChamadasDiagnostico { get; private set; }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            if (req.Metodo == "diagnosticarcausaraiz") ChamadasDiagnostico++;
            return Task.FromResult(RespostaIpc.Ok(req.Id, (object?)null));
        }
    }

    private sealed class AgenteDiagnosticoFake(
        IReadOnlyList<EventoInstabilidade> resultado, InfoBios? resultadoVerificarBios = null) : IRoteadorIpc
    {
        public List<InfoDriver> DriversRecebidos { get; } = new();
        public InfoBios? BiosRecebida { get; private set; }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            if (req.Metodo == "verificarbios")
                return Task.FromResult(RespostaIpc.Ok(req.Id, resultadoVerificarBios));

            if (req.Metodo != "diagnosticarcausaraiz")
                return Task.FromResult(RespostaIpc.Falha(req.Id, "método inesperado no fake"));

            if (req.Parametros is { } p)
            {
                if (p.TryGetProperty("driversDesatualizados", out var driversEl))
                {
                    var drivers = JsonSerializer.Deserialize<List<InfoDriver>>(driversEl.GetRawText(), ProtocoloIpc.Json) ?? [];
                    DriversRecebidos.AddRange(drivers);
                }

                if (p.TryGetProperty("bios", out var biosEl) && biosEl.ValueKind == JsonValueKind.Object)
                {
                    BiosRecebida = JsonSerializer.Deserialize<InfoBios>(biosEl.GetRawText(), ProtocoloIpc.Json);
                }
            }

            return Task.FromResult(RespostaIpc.Ok(req.Id, resultado));
        }
    }

    private sealed class AgenteDiagnosticoFalhaFake : IRoteadorIpc
    {
        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default) =>
            Task.FromResult(req.Metodo == "diagnosticarcausaraiz"
                ? RespostaIpc.Falha(req.Id, "falha simulada")
                : RespostaIpc.Ok(req.Id, (InfoBios?)null));
    }
}
