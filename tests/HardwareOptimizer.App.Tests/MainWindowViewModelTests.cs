using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class MainWindowViewModelTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Nome = "Windows 11" },
    };

    private static MatrizDecisao Matriz(NivelRisco risco) => new()
    {
        Origem = OrigemDecisao.Local,
        Itens = new[]
        {
            new ItemDecisao
            {
                AcaoId = "PWR_PLANO_ALTO_DESEMPENHO",
                Prioridade = 1,
                Categoria = CategoriaAcao.SistemaOperacional,
                Risco = risco,
                Justificativa = "energia",
            },
        },
    };

    [Fact]
    public async Task Coletar_atualiza_resumo_e_desocupa()
    {
        var vm = new MainWindowViewModel(new RoteadorFake(_ => RespostaIpc.Ok("1", Inventario())));

        await vm.ColetarCommand.ExecuteAsync(null);

        Assert.Contains("ASUS", vm.InventarioResumo, StringComparison.Ordinal);
        Assert.False(vm.Ocupado);
    }

    [Fact]
    public async Task Propor_preenche_a_matriz()
    {
        var vm = new MainWindowViewModel(new RoteadorFake(_ => RespostaIpc.Ok("1", Matriz(NivelRisco.MuitoBaixo))));

        await vm.ProporCommand.ExecuteAsync(null);

        Assert.Single(vm.Matriz);
        Assert.True(vm.Matriz[0].Selecionado); // risco muito baixo é pré-selecionado
    }

    [Fact]
    public async Task Aprovar_sem_selecao_avisa()
    {
        var vm = new MainWindowViewModel(new RoteadorFake(_ => RespostaIpc.Ok("1", Matriz(NivelRisco.Medio))));
        await vm.ProporCommand.ExecuteAsync(null);
        Assert.False(vm.Matriz[0].Selecionado); // risco médio não é pré-selecionado

        await vm.AprovarCommand.ExecuteAsync(null);

        Assert.Contains("Selecione", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aprovar_com_selecao_chama_o_metodo_aprovar()
    {
        string? metodoChamado = null;
        var vm = new MainWindowViewModel(new RoteadorFake(req =>
        {
            metodoChamado = req.Metodo;
            return req.Metodo == "proposta"
                ? RespostaIpc.Ok("1", Matriz(NivelRisco.MuitoBaixo))
                : RespostaIpc.Ok("2", new RelatorioExecucao { Sucesso = true, PerfilNome = "x" });
        }));

        await vm.ProporCommand.ExecuteAsync(null);
        await vm.AprovarCommand.ExecuteAsync(null);

        Assert.Equal("aprovar", metodoChamado);
        Assert.Contains("Aplicado", vm.ResultadoAprovacao, StringComparison.Ordinal);
    }

    private sealed class RoteadorFake : IRoteadorIpc
    {
        private readonly Func<RequisicaoIpc, RespostaIpc> _responder;

        public RoteadorFake(Func<RequisicaoIpc, RespostaIpc> responder) => _responder = responder;

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default) =>
            Task.FromResult(_responder(requisicao));
    }
}
