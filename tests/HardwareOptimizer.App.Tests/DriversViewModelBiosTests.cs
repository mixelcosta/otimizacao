using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-1-4-bios-alerta-risco (ponta a ponta na ViewModel).</summary>
public class DriversViewModelBiosTests
{
    private static PlacaMae Placa() => new()
    {
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoBios = "2806",
    };

    private static InfoBios Info() => new()
    {
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoAtual = "2806",
        VersaoDisponivel = "3405",
        UrlDownload = "https://www.asus.com/support/",
        TeclaSetup = "Del (ou F2)",
        Utilitario = "ASUS EZ Flash 3 (menu Tool/Advanced)",
        Passos = ["Passo 1", "Passo 2"],
        Avisos = ["Não desligue durante a gravação."],
    };

    // ── PopularBios / VerificarBiosAsync ────────────────────────────────────

    [Fact]
    public void PopularBios_ComAlerta_PopulaInfoBiosAtualETemAlerta()
    {
        var agente = new AgenteBiosFake(Info());
        var vm = new DriversViewModel(agente);

        vm.PopularBios(Placa());

        Assert.True(vm.TemBiosDesatualizada);
        Assert.NotNull(vm.InfoBiosAtual);
        Assert.Equal("ASUS", vm.InfoBiosAtual!.Fabricante);
        Assert.Equal("3405", vm.InfoBiosAtual.VersaoDisponivel);
    }

    [Fact]
    public void PopularBios_SemCoberturaOuJaAtualizada_NaoMostraAlerta()
    {
        var agente = new AgenteBiosFake(resultado: null);
        var vm = new DriversViewModel(agente);

        vm.PopularBios(Placa());

        Assert.False(vm.TemBiosDesatualizada);
        Assert.Null(vm.InfoBiosAtual);
    }

    [Fact]
    public void PopularBios_RespostaComFalha_NaoMostraAlerta()
    {
        var agente = new AgenteBiosFalhaFake();
        var vm = new DriversViewModel(agente);

        vm.PopularBios(Placa());

        Assert.False(vm.TemBiosDesatualizada);
        Assert.Null(vm.InfoBiosAtual);
    }

    [Fact]
    public void PopularBios_EnviaPlacaRecebida()
    {
        var agente = new AgenteBiosFake(Info());
        var vm = new DriversViewModel(agente);

        vm.PopularBios(Placa());

        Assert.NotNull(agente.PlacaRecebida);
        Assert.Equal("ASUS", agente.PlacaRecebida!.Fabricante);
        Assert.Equal("ROG STRIX B550-F", agente.PlacaRecebida.Modelo);
    }

    [Fact]
    public async Task VerificarBiosAsync_SemAgente_NaoFalha()
    {
        var vm = new DriversViewModel();
        vm.PopularBios(Placa());

        await vm.VerificarBiosCommand.ExecuteAsync(null);

        Assert.False(vm.TemBiosDesatualizada);
        Assert.Null(vm.InfoBiosAtual);
    }

    [Fact]
    public async Task VerificarBiosAsync_SemPlacaPopulada_NaoChamaAgente()
    {
        var agente = new AgenteBiosFake(Info());
        var vm = new DriversViewModel(agente);

        await vm.VerificarBiosCommand.ExecuteAsync(null);

        Assert.Equal(0, agente.Chamadas);
    }

    // ── AbrirConfirmacaoBios ─────────────────────────────────────────────────

    [Fact]
    public void AbrirConfirmacaoBios_AbrePainelComEstadoLimpoEMensagemDeRisco()
    {
        var vm = new DriversViewModel(new AgenteBiosFake(Info()));
        vm.PopularBios(Placa());

        vm.AbrirConfirmacaoBiosCommand.Execute(null);

        Assert.True(vm.PainelConfirmacaoBiosAberto);
        Assert.False(vm.ConfirmadoBios);
        Assert.False(vm.GuiaBiosVisivel);
        Assert.Contains("placa-mãe", vm.MensagemConfirmacaoBios, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("profissional", vm.MensagemConfirmacaoBios, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbrirConfirmacaoBios_SemAlertaAtivo_NaoAbrePainel()
    {
        var vm = new DriversViewModel();

        vm.AbrirConfirmacaoBiosCommand.Execute(null);

        Assert.False(vm.PainelConfirmacaoBiosAberto);
    }

    [Fact]
    public void AbrirConfirmacaoBios_SegundoClique_ResetaConfirmacaoMesmoJaTendoVisto()
    {
        var vm = new DriversViewModel(new AgenteBiosFake(Info()));
        vm.PopularBios(Placa());

        vm.AbrirConfirmacaoBiosCommand.Execute(null);
        vm.ConfirmadoBios = true;
        vm.VerGuiaBiosCommand.Execute(null);
        Assert.True(vm.GuiaBiosVisivel);

        // Segundo clique em "ver orientação" na mesma sessão.
        vm.AbrirConfirmacaoBiosCommand.Execute(null);

        Assert.True(vm.PainelConfirmacaoBiosAberto);
        Assert.False(vm.ConfirmadoBios, "a confirmação deve ser resetada mesmo já tendo sido vista nesta sessão");
        Assert.False(vm.GuiaBiosVisivel);
    }

    // ── VerGuiaBios ──────────────────────────────────────────────────────────

    [Fact]
    public void VerGuiaBios_ComConfirmacao_RevelaGuiaSemChamarAgente()
    {
        var agente = new AgenteBiosFake(Info());
        var vm = new DriversViewModel(agente);
        vm.PopularBios(Placa());
        var chamadasAntes = agente.Chamadas;

        vm.AbrirConfirmacaoBiosCommand.Execute(null);
        vm.ConfirmadoBios = true;
        vm.VerGuiaBiosCommand.Execute(null);

        Assert.True(vm.GuiaBiosVisivel);
        Assert.Equal(chamadasAntes, agente.Chamadas);
    }

    [Fact]
    public void VerGuiaBios_SemConfirmacao_NaoRevelaGuia()
    {
        var vm = new DriversViewModel(new AgenteBiosFake(Info()));
        vm.PopularBios(Placa());
        vm.AbrirConfirmacaoBiosCommand.Execute(null);

        vm.VerGuiaBiosCommand.Execute(null);

        Assert.False(vm.ConfirmadoBios);
        Assert.False(vm.GuiaBiosVisivel, "nunca deve revelar o guia sem a confirmação de risco (defesa em profundidade, mesmo padrão do driver)");
    }

    [Fact]
    public async Task VerificarBiosAsync_NovaVerificacao_ResetaConfirmacaoEGuiaJaAbertos()
    {
        var agente = new AgenteBiosFake(Info());
        var vm = new DriversViewModel(agente);
        vm.PopularBios(Placa());
        vm.AbrirConfirmacaoBiosCommand.Execute(null);
        vm.ConfirmadoBios = true;
        vm.VerGuiaBiosCommand.Execute(null);
        Assert.True(vm.GuiaBiosVisivel);

        // Uma nova verificação (ex.: novo SCAN) não pode herdar confirmação/guia
        // já revelados de uma verificação anterior.
        await vm.VerificarBiosCommand.ExecuteAsync(null);

        Assert.False(vm.PainelConfirmacaoBiosAberto);
        Assert.False(vm.ConfirmadoBios);
        Assert.False(vm.GuiaBiosVisivel);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class AgenteBiosFake(InfoBios? resultado) : IRoteadorIpc
    {
        public int Chamadas { get; private set; }
        public PlacaMae? PlacaRecebida { get; private set; }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            if (req.Metodo != "verificarbios")
                return Task.FromResult(RespostaIpc.Falha(req.Id, "método inesperado no fake"));

            Chamadas++;
            if (req.Parametros is { } p && p.TryGetProperty("placa", out var placaEl))
            {
                PlacaRecebida = System.Text.Json.JsonSerializer.Deserialize<PlacaMae>(
                    placaEl.GetRawText(), ProtocoloIpc.Json);
            }

            return Task.FromResult(RespostaIpc.Ok(req.Id, resultado));
        }
    }

    private sealed class AgenteBiosFalhaFake : IRoteadorIpc
    {
        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default) =>
            Task.FromResult(RespostaIpc.Falha(req.Id, "falha simulada"));
    }
}
