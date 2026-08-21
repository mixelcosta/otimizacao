using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.Tests;

public class DriversViewModelConfirmacaoTests
{
    private static InfoDriverViewModel DriverComAtualizacao() => new(new InfoDriver
    {
        HardwareId = "PCI\\VEN_10DE",
        Descricao = "NVIDIA RTX 4080",
        VersaoAtual = "531.0",
        VersaoDisponivel = "572.83",
        UrlDownload = "https://example.com/driver.inf",
        Status = StatusDriver.AtualizacaoDisponivel,
    });

    // ── AbrirConfirmacao ─────────────────────────────────────────────────────

    [Fact]
    public void AbrirConfirmacao_AbrePainelComEstadoLimpo()
    {
        var vm = new DriversViewModel();
        var driver = DriverComAtualizacao();

        vm.AbrirConfirmacaoCommand.Execute(driver);

        Assert.True(vm.PainelConfirmacaoAberto);
        Assert.Same(driver, vm.DriverSelecionado);
        Assert.False(vm.Confirmado);
        Assert.Null(vm.CaminhoBackupAtual);
        Assert.Contains(driver.Descricao, vm.MensagemConfirmacao);
    }

    [Fact]
    public void FecharConfirmacao_FechaPainelELimpaSelecao()
    {
        var vm = new DriversViewModel();
        vm.AbrirConfirmacaoCommand.Execute(DriverComAtualizacao());

        vm.FecharConfirmacaoCommand.Execute(null);

        Assert.False(vm.PainelConfirmacaoAberto);
        Assert.Null(vm.DriverSelecionado);
        Assert.False(vm.Confirmado);
    }

    // ── AplicarAtualizacaoAsync: botão de aplicar nunca dispara sem confirmação ──

    [Fact]
    public async Task AplicarAtualizacao_SemConfirmar_NaoChamaAgente()
    {
        var agente = new AgenteAprovacaoFake(new ResultadoAprovacaoDriverDto { Sucesso = true, CaminhoBackup = "/backup" });
        var vm = new DriversViewModel(agente);
        vm.AbrirConfirmacaoCommand.Execute(DriverComAtualizacao());
        // Confirmado permanece false (usuário não marcou a caixa no ConfirmationPanel).

        await vm.AplicarAtualizacaoCommand.ExecuteAsync(null);

        Assert.Equal(0, agente.Chamadas);
    }

    [Fact]
    public async Task AplicarAtualizacao_ComConfirmacaoEBackupOk_AtualizaCaminhoBackup()
    {
        var agente = new AgenteAprovacaoFake(new ResultadoAprovacaoDriverDto
        {
            Sucesso = true,
            CaminhoBackup = "/backup/2026-08-21",
        });
        var vm = new DriversViewModel(agente);
        vm.AbrirConfirmacaoCommand.Execute(DriverComAtualizacao());
        vm.Confirmado = true;

        await vm.AplicarAtualizacaoCommand.ExecuteAsync(null);

        Assert.Equal(1, agente.Chamadas);
        Assert.Equal("/backup/2026-08-21", vm.CaminhoBackupAtual);
        Assert.Contains("instalado", vm.StatusInstalacao, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.Instalando);
        Assert.False(vm.Confirmado, "cada tentativa exige nova confirmação explícita, mesmo após sucesso");
    }

    [Fact]
    public async Task AplicarAtualizacao_BackupFalhou_NaoInstalaMasMostraErro()
    {
        var agente = new AgenteAprovacaoFake(new ResultadoAprovacaoDriverDto
        {
            Sucesso = false,
            Erro = "Backup falhou: pnputil retornou código 1.",
            CaminhoBackup = null,
        });
        var vm = new DriversViewModel(agente);
        vm.AbrirConfirmacaoCommand.Execute(DriverComAtualizacao());
        vm.Confirmado = true;

        await vm.AplicarAtualizacaoCommand.ExecuteAsync(null);

        Assert.Null(vm.CaminhoBackupAtual);
        Assert.Contains("Falha", vm.StatusInstalacao);
        Assert.False(vm.Confirmado, "botão de aplicar deve voltar a exigir confirmação após falha de backup");
    }

    [Fact]
    public async Task AplicarAtualizacao_BackupOkMasInstalacaoFalha_MantemCaminhoBackupVisivel()
    {
        var agente = new AgenteAprovacaoFake(new ResultadoAprovacaoDriverDto
        {
            Sucesso = false,
            Erro = "pnputil /add-driver retornou código 1.",
            CaminhoBackup = "/backup/2026-08-21",
        });
        var vm = new DriversViewModel(agente);
        vm.AbrirConfirmacaoCommand.Execute(DriverComAtualizacao());
        vm.Confirmado = true;

        await vm.AplicarAtualizacaoCommand.ExecuteAsync(null);

        Assert.Equal("/backup/2026-08-21", vm.CaminhoBackupAtual);
        Assert.Contains("Falha", vm.StatusInstalacao);
        Assert.False(vm.Confirmado, "botão de aplicar deve voltar a exigir confirmação após falha de instalação");
    }

    // ── ReverterAsync: exige um backup já exportado nesta sessão do painel ──────

    [Fact]
    public async Task Reverter_SemCaminhoBackup_NaoChamaAgente()
    {
        var agente = new AgenteReversaoFake(sucesso: true);
        var vm = new DriversViewModel(agente);

        await vm.ReverterCommand.ExecuteAsync(null);

        Assert.Equal(0, agente.Chamadas);
    }

    [Fact]
    public async Task Reverter_ComCaminhoBackup_ChamaAgenteEAtualizaStatus()
    {
        var agente = new AgenteReversaoFake(sucesso: true);
        var vm = new DriversViewModel(agente);
        vm.AbrirConfirmacaoCommand.Execute(DriverComAtualizacao());
        vm.Confirmado = true;
        // Simula um backup já exportado nesta sessão do painel.
        vm.CaminhoBackupAtual = "/backup/2026-08-21";

        await vm.ReverterCommand.ExecuteAsync(null);

        Assert.Equal(1, agente.Chamadas);
        Assert.Contains("Rollback", vm.StatusInstalacao, StringComparison.OrdinalIgnoreCase);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class AgenteAprovacaoFake(ResultadoAprovacaoDriverDto resultado) : IRoteadorIpc
    {
        public int Chamadas { get; private set; }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            if (req.Metodo != "aprovaratualizacaodriver")
                return Task.FromResult(RespostaIpc.Falha(req.Id, "método inesperado no fake"));

            Chamadas++;
            return Task.FromResult(RespostaIpc.Ok(req.Id, resultado));
        }
    }

    private sealed class AgenteReversaoFake(bool sucesso) : IRoteadorIpc
    {
        public int Chamadas { get; private set; }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            if (req.Metodo != "reverteratualizacaodriver")
                return Task.FromResult(RespostaIpc.Falha(req.Id, "método inesperado no fake"));

            Chamadas++;
            var resp = sucesso
                ? RespostaIpc.Ok(req.Id, true)
                : RespostaIpc.Falha(req.Id, "Falha simulada de rollback.");
            return Task.FromResult(resp);
        }
    }
}
