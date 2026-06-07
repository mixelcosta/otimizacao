using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class VerificadorPreCondicoesTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static AcaoOtimizacao Acao(string id) =>
        Catalogo.Obter(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Backup_nao_confirmado_bloqueia()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("PWR_PLANO_ALTO_DESEMPENHO"),
            new Dictionary<string, string>(),
            new ContextoExecucao { BackupConfirmado = false });

        Assert.True(resultado.Falha);
    }

    [Fact]
    public void Backup_confirmado_aprova()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("PWR_PLANO_ALTO_DESEMPENHO"),
            new Dictionary<string, string>(),
            new ContextoExecucao { BackupConfirmado = true });

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Servico_fora_da_lista_segura_bloqueia()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("SRV_DESATIVAR_SERVICO"),
            new Dictionary<string, string> { ["nome_servico"] = "ServicoCritico" },
            new ContextoExecucao { BackupConfirmado = true });

        Assert.True(resultado.Falha);
    }

    [Fact]
    public void Servico_na_lista_segura_aprova()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("SRV_DESATIVAR_SERVICO"),
            new Dictionary<string, string> { ["nome_servico"] = "DiagTrack" },
            new ContextoExecucao { BackupConfirmado = true });

        Assert.True(resultado.Sucesso);
    }
}
