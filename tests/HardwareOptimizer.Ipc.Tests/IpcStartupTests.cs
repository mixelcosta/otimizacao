using System.Text.Json;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.Ipc.Tests;

public sealed class IpcStartupTests
{
    private static RoteadorIpc Roteador() => new(coletor: new ColetorFake());

    private static RequisicaoIpc Req(string metodo, object? parametros = null) => new()
    {
        Metodo = metodo,
        Parametros = parametros is null ? null : JsonSerializer.SerializeToElement(parametros),
    };

    [Fact]
    public async Task Aplicar_e_alias_valido_de_aprovar()
    {
        // "aplicar" é alias de "aprovar" — sem ações → falha com mensagem de ação vazia
        var r = await Roteador().TratarAsync(Req("aplicar", new { acoes = Array.Empty<string>() }));

        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task Aplicar_com_acao_valida_executa_sem_excecao()
    {
        var r = await Roteador().TratarAsync(Req("aplicar", new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO" } }));

        // Deve retornar sucesso ou falha controlada, nunca lançar exceção
        Assert.NotNull(r);
    }

    [Fact]
    public async Task ObterEntradasStartup_metodo_em_caixa_alta_funciona()
    {
        // Método passado pelo ViewModel em PascalCase; roteador normaliza para lower
        var r = await Roteador().TratarAsync(Req("ObterEntradasStartup"));

        if (OperatingSystem.IsWindows())
        {
            Assert.True(r.Sucesso);
            Assert.IsAssignableFrom<IReadOnlyList<InicializacaoEntrada>>(r.Resultado);
        }
        else
        {
            Assert.False(r.Sucesso);
            Assert.Contains("Windows", r.Erro, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DesativarStartup_sem_nome_retorna_falha()
    {
        // Parâmetro "nome" ausente → deve retornar falha descritiva
        var r = await Roteador().TratarAsync(Req("DesativarStartup"));

        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task DesativarStartup_nome_vazio_retorna_falha()
    {
        var r = await Roteador().TratarAsync(Req("DesativarStartup", new { nome = "" }));

        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task DesativarStartup_entrada_inexistente_retorna_falha()
    {
        // Entrada "AppTotalmenteInexistente" não está no registro → falha
        var r = await Roteador().TratarAsync(
            Req("DesativarStartup", new { nome = "AppTotalmenteInexistente_12345xyz" }));

        if (OperatingSystem.IsWindows())
        {
            Assert.False(r.Sucesso);
            Assert.NotNull(r.Erro);
        }
        else
        {
            Assert.False(r.Sucesso);
        }
    }

    [Fact]
    public async Task Metodo_desconhecido_ainda_falha_apos_extensoes()
    {
        var r = await Roteador().TratarAsync(Req("metodonaoexiste"));

        Assert.False(r.Sucesso);
        Assert.Contains("Método desconhecido", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ColetorFake : IColetorInventario
    {
        public Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Inventario
            {
                Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
                Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6 },
                Gpu = new[] { new PlacaVideo { Nome = "RTX 3060" } },
                SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
            });
    }
}
