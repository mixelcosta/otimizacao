using HardwareOptimizer.Agent.Bios;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ModuloBiosTests : IDisposable
{
    private readonly string _dir;

    public ModuloBiosTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-bios-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    private static Inventario Inventario(string fabricante, string modelo, string? versao) => new()
    {
        Placa = new PlacaMae { Fabricante = fabricante, Modelo = modelo, VersaoBios = versao, Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public async Task Placa_curada_desatualizada_recomenda_atualizacao()
    {
        var relatorio = await new ModuloBios().AnalisarAsync(
            Inventario("ASUSTeK Computer Inc.", "ROG STRIX B550-F", "2806"));

        Assert.True(relatorio.FonteEncontrada);
        Assert.True(relatorio.Decisao.RecomendaAtualizar);
        Assert.Equal("3405", relatorio.Decisao.VersaoRecomendada);
        Assert.Equal("ASUS", relatorio.Identificacao.Fabricante);
        Assert.Contains("EZ Flash", relatorio.Guia.Utilitario, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Placa_desconhecida_nao_encontra_fonte_e_nao_recomenda()
    {
        var relatorio = await new ModuloBios().AnalisarAsync(
            Inventario("Fabricante Genérico", "Placa Z", "1.0"));

        Assert.False(relatorio.FonteEncontrada);
        Assert.False(relatorio.Decisao.RecomendaAtualizar);
    }

    [Fact]
    public async Task Provedor_com_cache_consulta_interno_apenas_uma_vez()
    {
        var repo = RepositorioSqlite.DeArquivo(Path.Combine(_dir, "bios.db"));
        await repo.InicializarAsync();

        var interno = new ProvedorContador();
        var comCache = new ProvedorBiosComCache(interno, repo);

        var primeira = await comCache.ObterAsync("asus|rog strix b550-f");
        var segunda = await comCache.ObterAsync("asus|rog strix b550-f");

        Assert.NotNull(primeira);
        Assert.NotNull(segunda);
        Assert.Equal("3405", segunda!.VersaoMaisRecente);
        Assert.Equal(1, interno.Chamadas); // segunda veio do cache
    }

    private sealed class ProvedorContador : IProvedorInfoBios
    {
        private readonly BancoCuradoBios _curado = new();

        public int Chamadas { get; private set; }

        public Task<InfoBiosFabricante?> ObterAsync(string chaveBusca, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return _curado.ObterAsync(chaveBusca, cancellationToken);
        }
    }
}
