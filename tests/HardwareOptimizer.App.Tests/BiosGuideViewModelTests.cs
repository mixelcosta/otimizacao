using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.Tests;

public class BiosGuideViewModelTests
{
    // ── Estado inicial ─────────────────────────────────────────────────────────

    [Fact]
    public void EstadoInicial_SemAgente_ChatNaoDisponivel()
    {
        var vm = new BiosGuideViewModel();
        Assert.False(vm.ChatDisponivel);
    }

    [Fact]
    public void EstadoInicial_ComAgente_ChatDisponivel()
    {
        var vm = new BiosGuideViewModel(new AgenteIpcFake());
        Assert.True(vm.ChatDisponivel);
    }

    [Fact]
    public void EstadoInicial_PerguntaVazia_RespostaVazia_NaoPerguntando()
    {
        var vm = new BiosGuideViewModel(new AgenteIpcFake());
        Assert.Equal(string.Empty, vm.Pergunta);
        Assert.Equal(string.Empty, vm.RespostaIa);
        Assert.False(vm.Perguntando);
        Assert.False(vm.TemResposta);
    }

    // ── PerguntarCommand ──────────────────────────────────────────────────────

    [Fact]
    public async Task PerguntarAsync_PerguntaVazia_NaoAlteraPerguntandoNemResposta()
    {
        var agente = new AgenteIpcFake();
        var vm = new BiosGuideViewModel(agente);
        vm.Pergunta = "   ";

        await vm.PerguntarCommand.ExecuteAsync(null);

        Assert.False(vm.Perguntando);
        Assert.Equal(string.Empty, vm.RespostaIa);
        Assert.Equal(0, agente.Chamadas);
    }

    [Fact]
    public async Task PerguntarAsync_SemAgente_NaoLancaExcecao()
    {
        var vm = new BiosGuideViewModel(); // sem agente
        vm.Pergunta = "Como habilito XMP?";

        // Não deve lançar
        await vm.PerguntarCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.RespostaIa);
        Assert.False(vm.Perguntando);
    }

    [Fact]
    public async Task PerguntarAsync_ComPergunta_ChamaAgenteEPreencheResposta()
    {
        var agente = new AgenteIpcFake("Ative o XMP no menu AI Tweaker.");
        var vm = new BiosGuideViewModel(agente);
        vm.Pergunta = "Como habilito XMP na ASUS?";

        await vm.PerguntarCommand.ExecuteAsync(null);

        Assert.Equal(1, agente.Chamadas);
        Assert.Equal("Ative o XMP no menu AI Tweaker.", vm.RespostaIa);
        Assert.True(vm.TemResposta);
        Assert.False(vm.Perguntando);
    }

    [Fact]
    public async Task PerguntarAsync_AgenteRetornaFalha_MostraMensagemFallback()
    {
        var agente = new AgenteIpcFake(sucesso: false);
        var vm = new BiosGuideViewModel(agente);
        vm.Pergunta = "Como desativo CSM?";

        await vm.PerguntarCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.RespostaIa));
        Assert.False(vm.Perguntando);
    }

    [Fact]
    public async Task PerguntarAsync_SegundaChamada_SubstituiRespostaAnterior()
    {
        var agente = new AgenteIpcFake("Resposta 1");
        var vm = new BiosGuideViewModel(agente);

        vm.Pergunta = "Primeira pergunta";
        await vm.PerguntarCommand.ExecuteAsync(null);
        Assert.Equal("Resposta 1", vm.RespostaIa);

        agente.ProximaResposta = "Resposta 2";
        vm.Pergunta = "Segunda pergunta";
        await vm.PerguntarCommand.ExecuteAsync(null);
        Assert.Equal("Resposta 2", vm.RespostaIa);
        Assert.Equal(2, agente.Chamadas);
    }

    // ── Fake ──────────────────────────────────────────────────────────────────

    private sealed class AgenteIpcFake : IRoteadorIpc
    {
        private readonly bool _sucesso;
        public string ProximaResposta { get; set; }
        public int Chamadas { get; private set; }

        public AgenteIpcFake(string resposta = "OK", bool sucesso = true)
        {
            ProximaResposta = resposta;
            _sucesso = sucesso;
        }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            Chamadas++;
            var resp = _sucesso
                ? RespostaIpc.Ok(req.Id, ProximaResposta)
                : RespostaIpc.Falha(req.Id, "Erro simulado.");
            return Task.FromResult(resp);
        }
    }
}

public class DriversViewModelBackupTests
{
    [Fact]
    public void PodeExportarBackup_SemAgente_Falso()
    {
        var vm = new DriversViewModel();
        Assert.False(vm.PodeExportarBackup);
    }

    [Fact]
    public void PodeExportarBackup_ComAgente_Verdadeiro()
    {
        var vm = new DriversViewModel(new AgenteBackupFake());
        Assert.True(vm.PodeExportarBackup);
    }

    [Fact]
    public void EstadoInicial_NaoExportando_StatusVazio()
    {
        var vm = new DriversViewModel(new AgenteBackupFake());
        Assert.False(vm.Exportando);
        Assert.Equal(string.Empty, vm.BackupStatus);
    }

    [Fact]
    public async Task ExportarBackupCommand_Sucesso_AtualizaBackupStatus()
    {
        var agente = new AgenteBackupFake("/tmp/OtimizeBuilder/DriverBackups/2026-01-01");
        var vm = new DriversViewModel(agente);

        await vm.ExportarBackupCommand.ExecuteAsync(null);

        Assert.False(vm.Exportando);
        Assert.Contains("2026-01-01", vm.BackupStatus);
    }

    [Fact]
    public async Task ExportarBackupCommand_Falha_MostraMensagemErro()
    {
        var agente = new AgenteBackupFake(sucesso: false);
        var vm = new DriversViewModel(agente);

        await vm.ExportarBackupCommand.ExecuteAsync(null);

        Assert.False(vm.Exportando);
        Assert.Contains("Falha", vm.BackupStatus);
    }

    [Fact]
    public async Task ExportarBackupCommand_SemAgente_NaoAlteraStatus()
    {
        var vm = new DriversViewModel(); // sem agente
        await vm.ExportarBackupCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, vm.BackupStatus);
    }

    private sealed class AgenteBackupFake : IRoteadorIpc
    {
        private readonly string _pasta;
        private readonly bool _sucesso;

        public AgenteBackupFake(string pasta = "/drivers", bool sucesso = true)
        {
            _pasta   = pasta;
            _sucesso = sucesso;
        }

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            var resp = _sucesso
                ? RespostaIpc.Ok(req.Id, _pasta)
                : RespostaIpc.Falha(req.Id, "Erro simulado.");
            return Task.FromResult(resp);
        }
    }
}

public class InfoDriverViewModelDownloadTests
{
    [Fact]
    public void TemDownload_StatusAtualizacaoComUrl_Verdadeiro()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "NVIDIA RTX",
            Status = StatusDriver.AtualizacaoDisponivel,
            UrlDownload = "https://www.nvidia.com/drivers",
        });

        Assert.True(vm.TemDownload);
        Assert.Equal("https://www.nvidia.com/drivers", vm.UrlDownload);
    }

    [Fact]
    public void TemDownload_StatusAtualizado_Falso()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "NVIDIA RTX",
            Status = StatusDriver.Atualizado,
            UrlDownload = "https://www.nvidia.com/drivers",
        });

        Assert.False(vm.TemDownload);
    }

    [Fact]
    public void TemDownload_StatusAtualizacaoSemUrl_Falso()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "NVIDIA RTX",
            Status = StatusDriver.AtualizacaoDisponivel,
            UrlDownload = null,
        });

        Assert.False(vm.TemDownload);
    }

    [Fact]
    public void TemDownload_StatusDesconhecido_Falso()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_FFFF",
            Descricao = "Dispositivo",
            Status = StatusDriver.Desconhecido,
            UrlDownload = "https://exemplo.com",
        });

        Assert.False(vm.TemDownload);
    }

    [Fact]
    public void HardwareId_Preservado()
    {
        var hwid = "PCI\\VEN_10DE&DEV_2204";
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = hwid,
            Descricao = "NVIDIA",
            Status = StatusDriver.Desconhecido,
        });

        Assert.Equal(hwid, vm.HardwareId);
    }
}
