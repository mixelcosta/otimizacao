using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class IaCopilotoViewModelTests
{
    private static IRoteadorIpc RoteadorVazio() =>
        new RoteadorFake(_ => RespostaIpc.Ok("1", "resposta ia"));

    [Fact]
    public void Estado_inicial_sem_alerta_e_sem_mensagens()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());

        Assert.False(vm.TemAlertaPendente);
        Assert.Empty(vm.Mensagens);
    }

    [Fact]
    public void ReceberAlerta_adiciona_mensagem_no_topo()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());

        vm.ReceberAlerta("CPU 90°C — ventilação comprometida.");

        Assert.Single(vm.Mensagens);
        Assert.Equal(0, vm.Mensagens.IndexOf(vm.Mensagens[0]));
        Assert.Contains("CPU 90°C", vm.Mensagens[0].Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void ReceberAlerta_define_TemAlertaPendente_true()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());

        vm.ReceberAlerta("teste");

        Assert.True(vm.TemAlertaPendente);
    }

    [Fact]
    public void ReceberAlerta_mensagem_marcada_como_alerta()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());

        vm.ReceberAlerta("pico de ram");

        Assert.True(vm.Mensagens[0].EhAlerta);
    }

    [Fact]
    public void MarcarComoLido_limpa_TemAlertaPendente()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());
        vm.ReceberAlerta("qualquer coisa");

        vm.MarcarComoLido();

        Assert.False(vm.TemAlertaPendente);
    }

    [Fact]
    public void Multiplos_alertas_sao_inseridos_no_indice_0()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());

        vm.ReceberAlerta("Alerta 1");
        vm.ReceberAlerta("Alerta 2");

        // Alerta 2 deve ser o primeiro (índice 0)
        Assert.Contains("Alerta 2", vm.Mensagens[0].Texto, StringComparison.Ordinal);
        Assert.Contains("Alerta 1", vm.Mensagens[1].Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void ReceberAlerta_autor_e_Sistema()
    {
        var vm = new IaCopilotoViewModel(RoteadorVazio());

        vm.ReceberAlerta("teste autor");

        Assert.Equal("Sistema", vm.Mensagens[0].Autor);
    }

    private sealed class RoteadorFake : IRoteadorIpc
    {
        private readonly Func<RequisicaoIpc, RespostaIpc> _responder;
        public RoteadorFake(Func<RequisicaoIpc, RespostaIpc> responder) => _responder = responder;
        public Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default) =>
            Task.FromResult(_responder(requisicao));
    }
}
