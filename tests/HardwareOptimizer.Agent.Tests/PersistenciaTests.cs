using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class PersistenciaTests : IDisposable
{
    private readonly string _dir;

    public PersistenciaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-tests-" + Guid.NewGuid().ToString("N"));
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
            // Limpeza best-effort.
        }
    }

    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public async Task Repositorio_persiste_inventario_consentimento_e_execucao()
    {
        var repo = RepositorioSqlite.DeArquivo(Path.Combine(_dir, "otimizador.db"));
        await repo.InicializarAsync();

        await repo.SalvarInventarioAsync(Inventario());
        await repo.RegistrarConsentimentoAsync(new RegistroConsentimento
        {
            NomePerfil = "custom",
            VersaoCatalogo = "v1",
            CheckboxesMarcados = new[] { "aceite_riscos", "desejo_prosseguir" },
            ValoresEscolhidos = new[] { "SO_SYSTEM_RESPONSIVENESS.percentual_reserva = 5" },
        });
        await repo.RegistrarExecucaoAsync(new RelatorioExecucao { Sucesso = true, PerfilNome = "custom" });

        Assert.Equal(1, await repo.ContarInventariosAsync());
        Assert.Equal(1, await repo.ContarConsentimentosAsync());
        Assert.Equal(1, await repo.ContarExecucoesAsync());
    }

    [Fact]
    public async Task Backup_eh_confirmado_e_gravado_em_disco()
    {
        var servico = new ServicoBackup(Path.Combine(_dir, "backups"));

        var resultado = await servico.CriarBackupAsync(Inventario());

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.ValorObrigatorio.Confirmado);
        Assert.True(File.Exists(resultado.ValorObrigatorio.Caminho));
    }
}
