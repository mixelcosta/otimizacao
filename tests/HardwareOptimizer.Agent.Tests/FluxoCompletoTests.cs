using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Reporting;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Teste de integração que percorre TODOS os processos em sequência, como o
/// fluxo real: coleta -> sanitização -> perfil seguro -> backup -> execução
/// controlada -> relatório/score -> persistência.
/// </summary>
public sealed class FluxoCompletoTests : IDisposable
{
    private const string Serial = "SN-SEGREDO-123";
    private const string Uuid = "uuid-SEGREDO-abc";
    private const string Maquina = "PC-DO-USUARIO";
    private const string Usuario = "michel";
    private const string Mac = "AA:BB:CC:DD:EE:FF";

    private readonly string _dir;

    public FluxoCompletoTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-e2e-" + Guid.NewGuid().ToString("N"));
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

    private static Inventario InventarioRico() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "2806", Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6, Threads = 12, TempIdleC = 38 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 }, new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060", TempIdleC = 41, VersaoDriver = "551.23" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
        Rede = new[] { new InterfaceRede { Nome = "Ethernet", EnderecoMac = Mac } },
        Identificadores = new IdentificadoresSensiveis
        {
            NumeroSerie = Serial,
            UuidPlaca = Uuid,
            NomeMaquina = Maquina,
            NomeUsuario = Usuario,
            ChaveProdutoWindows = "AAAAA-BBBBB-CCCCC",
        },
    };

    [Fact]
    public async Task Fluxo_ponta_a_ponta_executa_todos_os_processos()
    {
        var catalogo = CatalogoPadrao.Criar();

        // 1) Coleta (via leitor injetado).
        var inventario = await new ColetorInventario(new LeitorFixo(InventarioRico())).ColetarAsync();

        // 2) Sanitização — nenhum segredo bruto pode sobrar no payload de nuvem.
        var sanitizacao = new Sanitizador("sal-fixo").Sanitizar(inventario);
        var jsonSeguro = JsonSerializer.Serialize(sanitizacao.InventarioSeguro);
        foreach (var segredo in new[] { Serial, Uuid, Maquina, Usuario, Mac })
        {
            Assert.DoesNotContain(segredo, jsonSeguro, StringComparison.OrdinalIgnoreCase);
        }

        // 3) Persistência do inventário + 7) auditoria.
        var repositorio = RepositorioSqlite.DeArquivo(Path.Combine(_dir, "fluxo.db"));
        await repositorio.InicializarAsync();
        await repositorio.SalvarInventarioAsync(inventario);

        // 4) Perfil seguro com TODAS as ações do catálogo.
        var construcao = new ConstrutorPerfil(catalogo)
            .CriarPerfilSeguro("e2e", catalogo.Todas.Select(a => a.Id));
        Assert.True(construcao.Sucesso, string.Join(" | ", construcao.Bloqueios));
        Assert.False(construcao.ExigeConsentimento); // perfil seguro não exige consentimento

        // 5) Backup obrigatório (bloqueante).
        var backup = await new ServicoBackup(Path.Combine(_dir, "backups")).CriarBackupAsync(inventario);
        Assert.True(backup.Sucesso);

        // 6) Execução controlada por categoria.
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            catalogo, RegistroComandos.Padrao(estado), new VerificadorPreCondicoes(), new ValidadorCategoriaSempreEstavel());
        var execucao = await executor.AplicarPerfilAsync(
            construcao.Perfil!, new ContextoExecucao { BackupConfirmado = backup.Sucesso });

        Assert.True(execucao.Sucesso);
        Assert.All(execucao.Categorias, c => Assert.Equal(SituacaoCategoria.Aplicada, c.Situacao));
        Assert.Equal("ALTO_DESEMPENHO", estado.Ler("powercfg:plano_ativo"));
        await repositorio.RegistrarExecucaoAsync(execucao);

        // 7) Relatório executivo e nota final.
        var validacoes = execucao.Categorias.Where(c => c.Validacao is not null).Select(c => c.Validacao!).ToList();
        var alteracoes = execucao.TodasAlteracoes.Select(a => new AlteracaoResumo(a.Alvo, a.ValorAnterior, a.ValorNovo)).ToList();
        var dominios = new HashSet<Dominio> { Dominio.Windows, Dominio.Gpu };
        var relatorio = new GeradorRelatorio().Gerar(inventario, validacoes, alteracoes, dominios);

        Assert.InRange(relatorio.NotaFinal, 0, 100);
        Assert.Equal(7, relatorio.Scores.Count);
        Assert.False(relatorio.RegressaoDetectada);

        // Auditoria persistida.
        Assert.Equal(1, await repositorio.ContarInventariosAsync());
        Assert.Equal(1, await repositorio.ContarExecucoesAsync());
    }

    private sealed class LeitorFixo : ILeitorPlataforma
    {
        private readonly Inventario _inventario;

        public LeitorFixo(Inventario inventario) => _inventario = inventario;

        public SistemaOperacionalTipo Tipo => _inventario.SistemaOperacional.Tipo;

        public Task<Inventario> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }
}
