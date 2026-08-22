using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.Tests;

public class DriversViewModelSoftwareTests
{
    [Fact]
    public async Task VerificarSoftwareAsync_ComResultado_PopulaListaESeusCampos()
    {
        var agente = new AgenteVerificacaoFake([
            new InfoSoftware
            {
                Nome = "7-Zip 21.07 (x64)",
                VersaoAtual = "21.07",
                VersaoDisponivel = "24.07",
                UrlDownload = "https://www.7-zip.org/",
                Status = StatusSoftware.AtualizacaoDisponivel,
            },
        ]);
        var vm = new DriversViewModel(agente);

        await vm.VerificarSoftwareCommand.ExecuteAsync(null);

        Assert.True(vm.TemResultadosSoftware);
        Assert.Single(vm.Software);
        Assert.Equal("7-Zip 21.07 (x64)", vm.Software[0].Nome);
        Assert.Equal("21.07", vm.Software[0].VersaoAtual);
        Assert.Equal("24.07", vm.Software[0].VersaoDisponivel);
        Assert.True(vm.Software[0].TemDownload);
    }

    [Fact]
    public async Task VerificarSoftwareAsync_SemAgente_NaoFalha()
    {
        var vm = new DriversViewModel();

        await vm.VerificarSoftwareCommand.ExecuteAsync(null);

        Assert.False(vm.TemResultadosSoftware);
        Assert.Empty(vm.Software);
    }

    [Fact]
    public async Task VerificarSoftwareAsync_ListaVazia_StatusIndicaNenhumEncontrado()
    {
        var agente = new AgenteVerificacaoFake([]);
        var vm = new DriversViewModel(agente);

        await vm.VerificarSoftwareCommand.ExecuteAsync(null);

        Assert.False(vm.TemResultadosSoftware);
        Assert.Contains("Nenhum", vm.StatusTextSoftware);
    }

    [Fact]
    public async Task VerificarSoftwareAsync_EnviaProgramasPopuladosViaPopularProgramas()
    {
        var agente = new AgenteVerificacaoFake([]);
        var vm = new DriversViewModel(agente);
        vm.PopularProgramas([
            new ProgramaInstalado { Nome = "7-Zip 21.07 (x64)", Versao = "21.07" },
        ]);

        await vm.VerificarSoftwareCommand.ExecuteAsync(null);

        Assert.Single(agente.ProgramasRecebidos);
        Assert.Equal("7-Zip 21.07 (x64)", agente.ProgramasRecebidos[0].Nome);
    }

    [Fact]
    public async Task VerificarSoftwareAsync_FalhaAposSucessoAnterior_LimpaListaEmVezDeMostrarDadoDesatualizado()
    {
        var agente = new AgenteVerificacaoSequencialFake(
            [new InfoSoftware { Nome = "7-Zip 21.07 (x64)", VersaoAtual = "21.07", VersaoDisponivel = "24.07", Status = StatusSoftware.AtualizacaoDisponivel }],
            respostaFalha: "erro simulado");
        var vm = new DriversViewModel(agente);

        await vm.VerificarSoftwareCommand.ExecuteAsync(null);
        Assert.True(vm.TemResultadosSoftware);
        Assert.Single(vm.Software);

        await vm.VerificarSoftwareCommand.ExecuteAsync(null);

        Assert.False(vm.TemResultadosSoftware);
        Assert.Empty(vm.Software);
        Assert.Contains("Falha", vm.StatusTextSoftware);
    }

    [Fact]
    public void AbrirDownloadSoftware_SemUrl_NaoLanca()
    {
        var vm = new DriversViewModel();
        var software = new InfoSoftwareViewModel(new InfoSoftware { Nome = "Programa Sem URL" });

        var ex = Record.Exception(() => vm.AbrirDownloadSoftwareCommand.Execute(software));

        Assert.Null(ex);
    }

    private sealed class AgenteVerificacaoFake(IReadOnlyList<InfoSoftware> resultado) : IRoteadorIpc
    {
        public List<ProgramaInstalado> ProgramasRecebidos { get; } = new();

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            if (req.Metodo != "verificarsoftware")
                return Task.FromResult(RespostaIpc.Falha(req.Id, "método inesperado no fake"));

            var programas = req.Parametros is { } p && p.TryGetProperty("programas", out var arr)
                ? System.Text.Json.JsonSerializer.Deserialize<List<ProgramaInstalado>>(arr.GetRawText(), ProtocoloIpc.Json) ?? []
                : [];
            ProgramasRecebidos.AddRange(programas);

            return Task.FromResult(RespostaIpc.Ok(req.Id, resultado));
        }
    }

    private sealed class AgenteVerificacaoSequencialFake(IReadOnlyList<InfoSoftware> primeiroResultado, string respostaFalha) : IRoteadorIpc
    {
        private int _chamadas;

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc req, CancellationToken ct = default)
        {
            _chamadas++;
            return Task.FromResult(_chamadas == 1
                ? RespostaIpc.Ok(req.Id, primeiroResultado)
                : RespostaIpc.Falha(req.Id, respostaFalha));
        }
    }
}
