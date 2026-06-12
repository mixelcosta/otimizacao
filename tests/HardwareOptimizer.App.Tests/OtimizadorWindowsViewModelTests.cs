using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class OtimizadorWindowsViewModelTests
{
    [Fact]
    public async Task VarrerStartup_com_entradas_popula_lista()
    {
        var entradas = new List<InicializacaoEntrada>
        {
            new() { Nome = "Spotify", Caminho = @"C:\Spotify\Spotify.exe", Impacto = ImpactoInicializacao.Medio, Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
            new() { Nome = "Steam", Caminho = @"C:\Steam\Steam.exe", Impacto = ImpactoInicializacao.Alto, Origem = OrigemInicializacao.RegistroMaquina, Ativo = true, ChaveRollback = "" },
        };

        var roteador = new RoteadorFake(req => req.Metodo.ToLowerInvariant() == "obterentradasstartup"
            ? RespostaIpc.Ok(req.Id, (IReadOnlyList<InicializacaoEntrada>)entradas)
            : RespostaIpc.Falha(req.Id, "inesperado"));

        var vm = new OtimizadorWindowsViewModel(roteador);

        await vm.VarrerStartupCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.EntradasStartup.Count);
        Assert.Contains(vm.EntradasStartup, e => e.Nome == "Spotify");
        Assert.Contains(vm.EntradasStartup, e => e.Nome == "Steam");
    }

    [Fact]
    public async Task VarrerStartup_sem_entradas_status_informa_zero()
    {
        var roteador = new RoteadorFake(req =>
            RespostaIpc.Ok(req.Id, (IReadOnlyList<InicializacaoEntrada>)new List<InicializacaoEntrada>()));

        var vm = new OtimizadorWindowsViewModel(roteador);

        await vm.VarrerStartupCommand.ExecuteAsync(null);

        Assert.Empty(vm.EntradasStartup);
        Assert.Contains("0", vm.StatusOtimizador, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VarrerStartup_atualiza_status_com_contagem()
    {
        var entradas = Enumerable.Range(0, 3).Select(i => new InicializacaoEntrada
        {
            Nome = $"App{i}",
            Caminho = "",
            Impacto = ImpactoInicializacao.Baixo,
            Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true,
            ChaveRollback = "",
        }).ToList();

        var roteador = new RoteadorFake(req =>
            RespostaIpc.Ok(req.Id, (IReadOnlyList<InicializacaoEntrada>)entradas));

        var vm = new OtimizadorWindowsViewModel(roteador);

        await vm.VarrerStartupCommand.ExecuteAsync(null);

        Assert.Contains("3", vm.StatusOtimizador, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DesativarEntrada_sucesso_define_ativo_false()
    {
        var roteador = new RoteadorFake(req => RespostaIpc.Ok(req.Id, true));

        var vm = new OtimizadorWindowsViewModel(roteador);
        var entradaVm = new InicializacaoEntradaViewModel(new InicializacaoEntrada
        {
            Nome = "Spotify",
            Caminho = @"C:\Spotify\Spotify.exe",
            Impacto = ImpactoInicializacao.Medio,
            Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true,
            ChaveRollback = "",
        });

        await vm.DesativarEntradaCommand.ExecuteAsync(entradaVm);

        Assert.False(entradaVm.Ativo);
    }

    [Fact]
    public async Task DesativarEntrada_falha_mantem_ativo_true()
    {
        var roteador = new RoteadorFake(req => RespostaIpc.Falha(req.Id, "entrada não encontrada"));

        var vm = new OtimizadorWindowsViewModel(roteador);
        var entradaVm = new InicializacaoEntradaViewModel(new InicializacaoEntrada
        {
            Nome = "Desconhecido",
            Caminho = "",
            Impacto = ImpactoInicializacao.Desconhecido,
            Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true,
            ChaveRollback = "",
        });

        await vm.DesativarEntradaCommand.ExecuteAsync(entradaVm);

        Assert.True(entradaVm.Ativo);
    }

    [Fact]
    public void InicializacaoEntradaViewModel_cor_impacto_alto_e_vermelho()
    {
        var entradaVm = new InicializacaoEntradaViewModel(new InicializacaoEntrada
        {
            Nome = "Steam",
            Caminho = "",
            Impacto = ImpactoInicializacao.Alto,
            Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true,
            ChaveRollback = "",
        });

        Assert.Equal("#FF3333", entradaVm.CorImpacto);
    }

    [Fact]
    public void InicializacaoEntradaViewModel_cor_impacto_medio_e_amarelo()
    {
        var entradaVm = new InicializacaoEntradaViewModel(new InicializacaoEntrada
        {
            Nome = "Spotify",
            Caminho = "",
            Impacto = ImpactoInicializacao.Medio,
            Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true,
            ChaveRollback = "",
        });

        Assert.Equal("#FFCC00", entradaVm.CorImpacto);
    }

    private sealed class RoteadorFake : IRoteadorIpc
    {
        private readonly Func<RequisicaoIpc, RespostaIpc> _responder;
        public RoteadorFake(Func<RequisicaoIpc, RespostaIpc> responder) => _responder = responder;
        public Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default) =>
            Task.FromResult(_responder(requisicao));
    }
}
