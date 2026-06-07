using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ValidacaoTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    // ---- ParserEstresse ------------------------------------------------------

    [Fact]
    public void Parser_le_saida_saudavel()
    {
        var medicao = new ParserEstresse().Parse(
            "WHEA errors: 0\nMax temperature: 78 C\nScore: 11850\nArtifacts: no\nBSOD: no");

        Assert.Equal(0, medicao.ErrosWhea);
        Assert.Equal(78, medicao.TempMaxC);
        Assert.Equal(11850, medicao.Pontuacao);
        Assert.False(medicao.TemFalhaCritica);
    }

    [Fact]
    public void Parser_detecta_sinais_de_falha()
    {
        var medicao = new ParserEstresse().Parse("WHEA errors: 3\nArtifacts: yes\nBSOD: yes\nMemory errors: 2");

        Assert.Equal(3, medicao.ErrosWhea);
        Assert.Equal(2, medicao.ErrosMemoria);
        Assert.True(medicao.Artefatos);
        Assert.True(medicao.TelaAzul);
        Assert.True(medicao.TemFalhaCritica);
    }

    // ---- AnalisadorRegressao -------------------------------------------------

    [Fact]
    public void Analisador_reprova_com_whea()
    {
        var medicao = new MedicaoEstresse { ErrosWhea = 3 };
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "OCCT", medicao, null, LimiaresValidacao.Padrao);

        Assert.True(r.Regressao);
        Assert.Equal("Reprovado", r.Estabilidade);
    }

    [Fact]
    public void Analisador_reprova_por_superaquecimento()
    {
        var medicao = new MedicaoEstresse { TempMaxC = 99 };
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "OCCT", medicao, null, LimiaresValidacao.Padrao);

        Assert.True(r.Regressao);
    }

    [Fact]
    public void Analisador_aprova_quando_saudavel()
    {
        var medicao = new MedicaoEstresse { TempMaxC = 78, Pontuacao = 11850 };
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "OCCT", medicao, null, LimiaresValidacao.Padrao);

        Assert.False(r.Regressao);
        Assert.Equal("Totalmente validado", r.Estabilidade);
    }

    [Fact]
    public void Analisador_detecta_queda_de_pontuacao_vs_baseline()
    {
        var baseline = new MedicaoEstresse { Pontuacao = 12000 };
        var atual = new MedicaoEstresse { Pontuacao = 9000 }; // queda > 5%
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "Cinebench", atual, baseline, LimiaresValidacao.Padrao);

        Assert.True(r.Regressao);
        Assert.NotNull(r.Antes);
    }

    // ---- RunnerValidacao -----------------------------------------------------

    [Fact]
    public async Task Runner_aprova_com_ferramenta_saudavel()
    {
        var r = await new RunnerValidacao(FerramentaEstresseSimulada.Saudavel())
            .ValidarAsync(CategoriaAcao.Cpu, Array.Empty<RegistroAlteracao>());

        Assert.False(r.Regressao);
    }

    [Fact]
    public async Task Runner_reprova_com_ferramenta_em_regressao()
    {
        var r = await new RunnerValidacao(FerramentaEstresseSimulada.ComRegressao("whea"))
            .ValidarAsync(CategoriaAcao.Cpu, Array.Empty<RegistroAlteracao>());

        Assert.True(r.Regressao);
    }

    // ---- Integração: regressão simulada reverte automaticamente --------------

    [Fact]
    public async Task Executor_com_runner_reverte_categoria_em_regressao()
    {
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            Catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.ComRegressao("bsod")));

        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, new ContextoExecucao { BackupConfirmado = true });

        Assert.False(relatorio.Sucesso);
        Assert.Equal(SituacaoCategoria.Revertida, relatorio.Categorias.Single().Situacao);
        Assert.Null(estado.Ler("registro:SystemResponsiveness")); // rollback automático restaurou o estado
    }

    [Fact]
    public async Task Executor_com_runner_saudavel_aplica_categoria()
    {
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            Catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel()));

        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, new ContextoExecucao { BackupConfirmado = true });

        Assert.True(relatorio.Sucesso);
        Assert.Equal("20", estado.Ler("registro:SystemResponsiveness"));
    }
}
