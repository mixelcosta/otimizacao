using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class OtimizadorWindowsViewModelTests
{
    // ── Startup ───────────────────────────────────────────────────────────

    [Fact]
    public void Popular_com_entradas_popula_lista()
    {
        var entradas = new List<InicializacaoEntrada>
        {
            new() { Nome = "Spotify", Caminho = @"C:\Spotify\Spotify.exe", Impacto = ImpactoInicializacao.Medio, Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
            new() { Nome = "Steam",   Caminho = @"C:\Steam\Steam.exe",   Impacto = ImpactoInicializacao.Alto,  Origem = OrigemInicializacao.RegistroMaquina, Ativo = true, ChaveRollback = "" },
        };

        var vm = new OtimizadorWindowsViewModel(new RoteadorFake(_ => RespostaIpc.Ok("x", true)));
        vm.Popular(entradas);

        Assert.Equal(2, vm.EntradasStartup.Count);
        Assert.Contains(vm.EntradasStartup, e => e.Nome == "Spotify");
        Assert.Contains(vm.EntradasStartup, e => e.Nome == "Steam");
    }

    [Fact]
    public void Popular_sem_entradas_lista_vazia()
    {
        var vm = new OtimizadorWindowsViewModel(new RoteadorFake(_ => RespostaIpc.Ok("x", true)));
        vm.Popular([]);

        Assert.Empty(vm.EntradasStartup);
    }

    [Fact]
    public void Popular_ordena_por_impacto_decrescente()
    {
        var entradas = new List<InicializacaoEntrada>
        {
            new() { Nome = "Baixo",  Caminho = "", Impacto = ImpactoInicializacao.Baixo, Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
            new() { Nome = "Alto",   Caminho = "", Impacto = ImpactoInicializacao.Alto,  Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
            new() { Nome = "Medio",  Caminho = "", Impacto = ImpactoInicializacao.Medio, Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
        };

        var vm = new OtimizadorWindowsViewModel(new RoteadorFake(_ => RespostaIpc.Ok("x", true)));
        vm.Popular(entradas);

        Assert.Equal("Alto",  vm.EntradasStartup[0].Nome);
        Assert.Equal("Medio", vm.EntradasStartup[1].Nome);
        Assert.Equal("Baixo", vm.EntradasStartup[2].Nome);
    }

    [Fact]
    public async Task Toggle_entrada_sucesso_inverte_ativo()
    {
        var roteador = new RoteadorFake(req => RespostaIpc.Ok(req.Id, true));
        var vm = new OtimizadorWindowsViewModel(roteador);

        var entrada = new InicializacaoEntrada
        {
            Nome = "Spotify", Caminho = @"C:\Spotify\Spotify.exe",
            Impacto = ImpactoInicializacao.Medio, Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true, ChaveRollback = "",
        };
        vm.Popular([entrada]);

        var entradaVm = vm.EntradasStartup[0];
        Assert.True(entradaVm.Ativo);

        await entradaVm.ToggleCommand.ExecuteAsync(null);

        Assert.False(entradaVm.Ativo);
    }

    [Fact]
    public async Task Toggle_entrada_falha_mantem_estado()
    {
        var roteador = new RoteadorFake(req => RespostaIpc.Falha(req.Id, "acesso negado"));
        var vm = new OtimizadorWindowsViewModel(roteador);

        var entrada = new InicializacaoEntrada
        {
            Nome = "Desconhecido", Caminho = "",
            Impacto = ImpactoInicializacao.Desconhecido, Origem = OrigemInicializacao.RegistroUsuario,
            Ativo = true, ChaveRollback = "",
        };
        vm.Popular([entrada]);

        var entradaVm = vm.EntradasStartup[0];
        await entradaVm.ToggleCommand.ExecuteAsync(null);

        Assert.True(entradaVm.Ativo);
    }

    // ── InicializacaoEntradaViewModel ─────────────────────────────────────

    [Fact]
    public void InicializacaoEntradaViewModel_cor_impacto_alto_e_vermelho()
    {
        var entradaVm = new InicializacaoEntradaViewModel(
            new InicializacaoEntrada { Nome = "Steam", Caminho = "", Impacto = ImpactoInicializacao.Alto, Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
            _ => Task.CompletedTask);

        Assert.Equal("#FF4444", entradaVm.CorImpacto);
    }

    [Fact]
    public void InicializacaoEntradaViewModel_cor_impacto_medio_e_amarelo()
    {
        var entradaVm = new InicializacaoEntradaViewModel(
            new InicializacaoEntrada { Nome = "Spotify", Caminho = "", Impacto = ImpactoInicializacao.Medio, Origem = OrigemInicializacao.RegistroUsuario, Ativo = true, ChaveRollback = "" },
            _ => Task.CompletedTask);

        Assert.Equal("#FFCC00", entradaVm.CorImpacto);
    }

    // ── Serviços ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CarregarServicos_sucesso_popula_lista()
    {
        var servicos = new List<ServicoWindows>
        {
            new() { Nome = "wuauserv",   Descricao = "Windows Update",    Status = "Running", Pid = 1234 },
            new() { Nome = "spooler",    Descricao = "Print Spooler",     Status = "Stopped", Pid = 0    },
            new() { Nome = "LanmanServer", Descricao = "Server",          Status = "Running", Pid = 500  },
        };

        var roteador = new RoteadorFake(req => req.Metodo == "obterservicos"
            ? RespostaIpc.Ok(req.Id, (IReadOnlyList<ServicoWindows>)servicos)
            : RespostaIpc.Falha(req.Id, "inesperado"));

        var vm = new OtimizadorWindowsViewModel(roteador);
        vm.SubPagina = SubPaginaOtimizador.Servicos;
        await Task.Delay(50);

        Assert.Equal(3, vm.ServicosFiltrados.Count);
    }

    [Fact]
    public async Task FiltroServicos_filtra_por_nome()
    {
        var servicos = new List<ServicoWindows>
        {
            new() { Nome = "wuauserv", Descricao = "Windows Update", Status = "Running", Pid = 1 },
            new() { Nome = "spooler",  Descricao = "Print Spooler",  Status = "Stopped", Pid = 0 },
        };

        var roteador = new RoteadorFake(req => req.Metodo == "obterservicos"
            ? RespostaIpc.Ok(req.Id, (IReadOnlyList<ServicoWindows>)servicos)
            : RespostaIpc.Falha(req.Id, "inesperado"));

        var vm = new OtimizadorWindowsViewModel(roteador);
        vm.SubPagina = SubPaginaOtimizador.Servicos;
        await Task.Delay(50);

        vm.FiltroServicos = "spooler";

        Assert.Single(vm.ServicosFiltrados);
        Assert.Equal("spooler", vm.ServicosFiltrados[0].Nome);
    }

    [Fact]
    public async Task ToggleServico_running_para_servico()
    {
        string? metodoEnviado = null;
        var servicos = new List<ServicoWindows>
        {
            new() { Nome = "spooler", Descricao = "Print Spooler", Status = "Running", Pid = 12 },
        };

        var roteador = new RoteadorFake(req =>
        {
            metodoEnviado = req.Metodo;
            return req.Metodo == "obterservicos"
                ? RespostaIpc.Ok(req.Id, (IReadOnlyList<ServicoWindows>)servicos)
                : RespostaIpc.Ok(req.Id, true);
        });

        var vm = new OtimizadorWindowsViewModel(roteador);
        vm.SubPagina = SubPaginaOtimizador.Servicos;
        await Task.Delay(50);

        await vm.ServicosFiltrados[0].ToggleCommand.ExecuteAsync(null);

        Assert.Equal("pararservico", metodoEnviado);
        Assert.Equal("Stopped", vm.ServicosFiltrados[0].Status);
    }

    // ── ServicoViewModel ─────────────────────────────────────────────────

    [Fact]
    public void ServicoViewModel_running_exibe_botao_parar()
    {
        var svc = new ServicoViewModel(
            new ServicoWindows { Nome = "spooler", Descricao = "Print Spooler", Status = "Running", Pid = 1 },
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        Assert.Equal("PARAR", svc.TextoBotao);
        Assert.True(svc.Rodando);
    }

    [Fact]
    public void ServicoViewModel_stopped_exibe_botao_iniciar()
    {
        var svc = new ServicoViewModel(
            new ServicoWindows { Nome = "spooler", Descricao = "Print Spooler", Status = "Stopped", Pid = 0 },
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        Assert.Equal("INICIAR", svc.TextoBotao);
        Assert.False(svc.Rodando);
    }

    [Fact]
    public void ServicoViewModel_pid_zero_exibe_traco()
    {
        var svc = new ServicoViewModel(
            new ServicoWindows { Nome = "spooler", Descricao = "Print Spooler", Status = "Stopped", Pid = 0 },
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        Assert.Equal("—", svc.PidTexto);
    }

    [Fact]
    public void ServicoViewModel_modo_auto_converte_para_portugues()
    {
        var svc = new ServicoViewModel(
            new ServicoWindows { Nome = "wuauserv", Descricao = "Windows Update", Status = "Running", Pid = 1, ModoInicio = "Auto" },
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        Assert.Equal("Automático", svc.ModoInicioSelecionado);
    }

    [Fact]
    public void ServicoViewModel_modo_disabled_converte_para_portugues()
    {
        var svc = new ServicoViewModel(
            new ServicoWindows { Nome = "spooler", Descricao = "Print Spooler", Status = "Stopped", Pid = 0, ModoInicio = "Disabled" },
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        Assert.Equal("Desativado", svc.ModoInicioSelecionado);
    }

    [Fact]
    public async Task AlterarModoInicio_chama_rota_correta()
    {
        string? metodoCapturado = null;
        string? modoCapturado   = null;

        var servicos = new List<ServicoWindows>
        {
            new() { Nome = "spooler", Descricao = "Print Spooler", Status = "Stopped", Pid = 0, ModoInicio = "Auto" },
        };

        var roteador = new RoteadorFake(req =>
        {
            metodoCapturado = req.Metodo;
            if (req.Parametros is { } p && p.TryGetProperty("modo", out var m))
                modoCapturado = m.GetString();
            return req.Metodo == "obterservicos"
                ? RespostaIpc.Ok(req.Id, (IReadOnlyList<ServicoWindows>)servicos)
                : RespostaIpc.Ok(req.Id, true);
        });

        var vm = new OtimizadorWindowsViewModel(roteador);
        vm.SubPagina = SubPaginaOtimizador.Servicos;
        await Task.Delay(50);

        vm.ServicosFiltrados[0].ModoInicioSelecionado = "Desativado";
        await Task.Delay(50);

        Assert.Equal("alterarmododeinicio", metodoCapturado);
        Assert.Equal("Desativado", modoCapturado);
    }

    private sealed class RoteadorFake : IRoteadorIpc
    {
        private readonly Func<RequisicaoIpc, RespostaIpc> _responder;
        public RoteadorFake(Func<RequisicaoIpc, RespostaIpc> responder) => _responder = responder;
        public Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default) =>
            Task.FromResult(_responder(requisicao));
    }
}
