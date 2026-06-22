using System.Text.Json;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.Ipc.Tests;

public sealed class IpcTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6 },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    private static RoteadorIpc Roteador() => new(coletor: new ColetorFake(Inventario()));

    private static RequisicaoIpc Req(string metodo, object? parametros = null) => new()
    {
        Metodo = metodo,
        Parametros = parametros is null ? null : JsonSerializer.SerializeToElement(parametros),
    };

    // ---- RoteadorIpc (unitário) ----------------------------------------------

    [Fact]
    public async Task Ping_responde_pong()
    {
        var r = await Roteador().TratarAsync(Req("ping"));
        Assert.True(r.Sucesso);
        Assert.Equal("pong", r.Resultado);
    }

    [Fact]
    public async Task Metodo_desconhecido_falha()
    {
        var r = await Roteador().TratarAsync(Req("inexistente"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task Catalogo_retorna_lista_de_acoes()
    {
        var r = await Roteador().TratarAsync(Req("catalogo"));

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<AcaoResumoDto>>(r.Resultado);
        Assert.NotEmpty(lista);
        Assert.Contains(lista, a => a.Id == "PWR_PLANO_ALTO_DESEMPENHO");
    }

    [Fact]
    public async Task Coletar_retorna_o_inventario()
    {
        var r = await Roteador().TratarAsync(Req("coletar"));

        Assert.True(r.Sucesso);
        var inv = Assert.IsType<Inventario>(r.Resultado);
        Assert.Equal("ASUS", inv.Placa.Fabricante);
    }

    [Fact]
    public async Task Proposta_retorna_matriz_de_decisao()
    {
        var r = await Roteador().TratarAsync(Req("proposta"));

        Assert.True(r.Sucesso);
        var matriz = Assert.IsType<Cerebro.MatrizDecisao>(r.Resultado);
        Assert.NotEmpty(matriz.Itens);
    }

    [Fact]
    public async Task Aprovar_acoes_executa_e_retorna_relatorio()
    {
        var r = await Roteador().TratarAsync(Req("aprovar", new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO" } }));

        Assert.True(r.Sucesso);
        var relatorio = Assert.IsType<Agent.Execution.RelatorioExecucao>(r.Resultado);
        Assert.True(relatorio.Sucesso);
    }

    [Fact]
    public async Task Aprovar_sem_acoes_falha()
    {
        var r = await Roteador().TratarAsync(Req("aprovar", new { acoes = Array.Empty<string>() }));
        Assert.False(r.Sucesso);
    }

    // ---- Loopback real de named pipe -----------------------------------------

    [Fact]
    public async Task NamedPipe_loopback_responde_requisicoes()
    {
        var nome = "hwopt-test-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var servidor = new ServidorNamedPipe(nome, Roteador());
        var tarefaServidor = servidor.ServirAsync(cts.Token);

        var cliente = new ClienteNamedPipe(nome);
        var ping = await cliente.ChamarAsync("ping", cts.Token);
        var catalogo = await cliente.ChamarAsync("catalogo", cts.Token);

        Assert.True(ping.Sucesso);
        Assert.True(catalogo.Sucesso);

        await cts.CancelAsync();
        try
        {
            await tarefaServidor;
        }
        catch (OperationCanceledException)
        {
            // encerramento esperado
        }
    }

    // ---- exportarbackupdrivers --------------------------------------------------

    [Fact]
    public async Task ExportarBackupDrivers_NaoWindows_RetornaFalha()
    {
        if (OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("exportarbackupdrivers"));
        Assert.False(r.Sucesso);
        Assert.Contains("Windows", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportarBackupDrivers_Windows_CriaSubpasta()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("exportarbackupdrivers"));

        var raiz = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OtimizeBuilder", "DriverBackups");

        // A pasta base sempre deve ser criada, independentemente do pnputil ter êxito.
        Assert.True(Directory.Exists(raiz), $"Esperava: {raiz}");

        // Se pnputil teve êxito, Resultado deve ser a pasta com timestamp.
        if (r.Sucesso)
            Assert.IsType<string>(r.Resultado);
    }

    private sealed class ColetorFake : IColetorInventario
    {
        private readonly Inventario _inventario;

        public ColetorFake(Inventario inventario) => _inventario = inventario;

        public Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }
}
