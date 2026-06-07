using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ExecutorControladoTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static (ExecutorControlado Executor, EstadoSistemaSimulado Estado) Montar(
        IValidadorCategoria? validador = null)
    {
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            Catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            validador ?? new ValidadorCategoriaSempreEstavel());
        return (executor, estado);
    }

    private static ContextoExecucao ComBackup() => new() { BackupConfirmado = true };

    [Fact]
    public async Task Perfil_seguro_com_backup_aplica_e_grava_estado()
    {
        var (executor, estado) = Montar();
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS", "PWR_PLANO_ALTO_DESEMPENHO" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        Assert.True(relatorio.Sucesso);
        Assert.All(relatorio.Categorias, c => Assert.Equal(SituacaoCategoria.Aplicada, c.Situacao));
        Assert.Equal("20", estado.Ler("registro:SystemResponsiveness"));
        Assert.Equal("ALTO_DESEMPENHO", estado.Ler("powercfg:plano_ativo"));
    }

    [Fact]
    public async Task Sem_backup_confirmado_categoria_eh_bloqueada()
    {
        var (executor, estado) = Montar();
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, new ContextoExecucao { BackupConfirmado = false });

        Assert.False(relatorio.Sucesso);
        Assert.Equal(SituacaoCategoria.Bloqueada, relatorio.Categorias.Single().Situacao);
        Assert.Null(estado.Ler("registro:SystemResponsiveness")); // nada foi gravado.
    }

    [Fact]
    public async Task Regressao_reverte_categoria_e_restaura_estado()
    {
        var (executor, estado) = Montar(new ValidadorComRegressao(CategoriaAcao.SistemaOperacional));
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        Assert.False(relatorio.Sucesso);
        Assert.Equal(SituacaoCategoria.Revertida, relatorio.Categorias.Single().Situacao);
        Assert.Null(estado.Ler("registro:SystemResponsiveness")); // rollback restaurou o estado.
    }

    [Fact]
    public async Task Perfil_customizado_sem_consentimento_eh_bloqueado()
    {
        var (executor, _) = Montar();
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilCustomizado(
                "custom", "usuario",
                new[] { new SelecaoAcao { AcaoId = "SO_SYSTEM_RESPONSIVENESS", Parametros = Par("percentual_reserva", "5") } })
            .Perfil!;

        Assert.False(perfil.ConsentimentoRegistrado);

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        Assert.False(relatorio.Sucesso);
        Assert.Empty(relatorio.Categorias);
    }

    [Fact]
    public async Task Categorias_sao_aplicadas_na_ordem_do_documento()
    {
        var (executor, _) = Montar();
        // Seleção fora de ordem: Rede, GPU, Serviços, Sistema Operacional.
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[]
            {
                "NET_THROTTLING_DESABILITAR", "GPU_HAGS", "SRV_DESATIVAR_SERVICO", "PWR_PLANO_ALTO_DESEMPENHO",
            })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        var ordem = relatorio.Categorias.Select(c => c.Categoria).ToArray();
        Assert.Equal(
            new[] { CategoriaAcao.Gpu, CategoriaAcao.SistemaOperacional, CategoriaAcao.Servicos, CategoriaAcao.Rede },
            ordem);
    }

    [Fact]
    public async Task Registro_de_alteracao_guarda_valor_anterior_e_novo()
    {
        var estadoInicial = new Dictionary<string, string> { ["registro:SystemResponsiveness"] = "20" };
        var estado = new EstadoSistemaSimulado(estadoInicial);
        var executor = new ExecutorControlado(
            Catalogo, RegistroComandos.Padrao(estado), new VerificadorPreCondicoes(), new ValidadorCategoriaSempreEstavel());

        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilCustomizado(
                "custom", "usuario",
                new[] { new SelecaoAcao { AcaoId = "SO_SYSTEM_RESPONSIVENESS", Parametros = Par("percentual_reserva", "10") } })
            .Perfil! with
            { ConsentimentoRegistrado = true };

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        var alteracao = relatorio.TodasAlteracoes.Single();
        Assert.Equal("20", alteracao.ValorAnterior);
        Assert.Equal("10", alteracao.ValorNovo);
    }

    private static Dictionary<string, string> Par(string nome, string valor) =>
        new(StringComparer.OrdinalIgnoreCase) { [nome] = valor };

    private sealed class ValidadorComRegressao : IValidadorCategoria
    {
        private readonly CategoriaAcao _categoriaComRegressao;

        public ValidadorComRegressao(CategoriaAcao categoria) => _categoriaComRegressao = categoria;

        public Task<ResultadoValidacao> ValidarAsync(
            CategoriaAcao categoria,
            IReadOnlyList<RegistroAlteracao> alteracoes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoValidacao
            {
                Categoria = categoria.ToString(),
                Ferramenta = "fake",
                Regressao = categoria == _categoriaComRegressao,
                Estabilidade = categoria == _categoriaComRegressao ? "Reprovado" : "OK",
            });
    }
}
