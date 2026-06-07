using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Aplica um perfil aprovado, uma categoria por vez, na ordem do documento.
/// Após cada categoria, valida a estabilidade; se reprovar (ou se uma
/// pré-condição falhar), reverte a categoria inteira pelo registro de
/// alterações. Executa somente comandos do registro — nada fora do catálogo.
/// </summary>
public sealed class ExecutorControlado
{
    private readonly CatalogoAcoes _catalogo;
    private readonly RegistroComandos _comandos;
    private readonly IVerificadorPreCondicoes _preCondicoes;
    private readonly IValidadorCategoria _validadorCategoria;

    public ExecutorControlado(
        CatalogoAcoes catalogo,
        RegistroComandos comandos,
        IVerificadorPreCondicoes preCondicoes,
        IValidadorCategoria validadorCategoria)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(comandos);
        ArgumentNullException.ThrowIfNull(preCondicoes);
        ArgumentNullException.ThrowIfNull(validadorCategoria);

        _catalogo = catalogo;
        _comandos = comandos;
        _preCondicoes = preCondicoes;
        _validadorCategoria = validadorCategoria;
    }

    public async Task<RelatorioExecucao> AplicarPerfilAsync(
        Perfil perfil, ContextoExecucao contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        // Perfil customizado só pode ser aplicado após consentimento registrado.
        if (!perfil.PodeAplicar)
        {
            return new RelatorioExecucao
            {
                Sucesso = false,
                PerfilNome = perfil.Nome,
                Mensagens = new[] { "Perfil customizado sem consentimento registrado: execução bloqueada." },
            };
        }

        var categorias = new List<ResultadoCategoria>();
        var sucessoGeral = true;

        // Agrupa por categoria e ordena pela ordem natural do enum (= categorias_ordem).
        var grupos = perfil.Selecoes
            .Select(selecao => (selecao, acao: _catalogo.Obter(selecao.AcaoId)))
            .Where(par => par.acao is not null)
            .GroupBy(par => par.acao!.Categoria)
            .OrderBy(grupo => grupo.Key);

        foreach (var grupo in grupos)
        {
            var resultado = await AplicarCategoriaAsync(grupo.Key, grupo, contexto, cancellationToken)
                .ConfigureAwait(false);
            categorias.Add(resultado);

            if (resultado.Situacao != SituacaoCategoria.Aplicada)
            {
                sucessoGeral = false;
            }
        }

        return new RelatorioExecucao
        {
            Sucesso = sucessoGeral,
            PerfilNome = perfil.Nome,
            Categorias = categorias,
        };
    }

    private async Task<ResultadoCategoria> AplicarCategoriaAsync(
        CategoriaAcao categoria,
        IEnumerable<(SelecaoAcao Selecao, AcaoOtimizacao? Acao)> itens,
        ContextoExecucao contexto,
        CancellationToken cancellationToken)
    {
        var aplicadas = new List<RegistroAlteracao>();
        var mensagens = new List<string>();

        foreach (var (selecao, acao) in itens)
        {
            var pre = _preCondicoes.Verificar(acao!, selecao.Parametros, contexto);
            if (pre.Falha)
            {
                mensagens.AddRange(pre.Erros);
                return await ReverterCategoriaAsync(
                    categoria, aplicadas, SituacaoCategoria.Bloqueada, mensagens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var comando = _comandos.Obter(acao!.ComandoInternoId);
            if (comando is null)
            {
                mensagens.Add($"Ação '{acao.Id}': comando interno '{acao.ComandoInternoId}' não registrado.");
                return await ReverterCategoriaAsync(
                    categoria, aplicadas, SituacaoCategoria.Bloqueada, mensagens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var registro = await comando
                .AplicarAsync(acao.Id, categoria, selecao.Parametros, cancellationToken)
                .ConfigureAwait(false);
            aplicadas.Add(registro);
        }

        // Validação por categoria (runner de testes). Reprovou -> reverte tudo.
        var validacao = await _validadorCategoria
            .ValidarAsync(categoria, aplicadas, cancellationToken)
            .ConfigureAwait(false);

        if (validacao.Regressao)
        {
            mensagens.Add($"Regressão detectada na categoria {categoria}: revertendo.");
            var revertida = await ReverterCategoriaAsync(
                categoria, aplicadas, SituacaoCategoria.Revertida, mensagens, cancellationToken)
                .ConfigureAwait(false);
            return revertida with { Validacao = validacao };
        }

        return new ResultadoCategoria
        {
            Categoria = categoria,
            Situacao = SituacaoCategoria.Aplicada,
            Alteracoes = aplicadas,
            Validacao = validacao,
        };
    }

    private async Task<ResultadoCategoria> ReverterCategoriaAsync(
        CategoriaAcao categoria,
        List<RegistroAlteracao> aplicadas,
        SituacaoCategoria situacao,
        List<string> mensagens,
        CancellationToken cancellationToken)
    {
        var revertidas = new List<RegistroAlteracao>(aplicadas.Count);

        // Reverte na ordem inversa da aplicação.
        for (var i = aplicadas.Count - 1; i >= 0; i--)
        {
            var registro = aplicadas[i];
            var comando = _comandos.Obter(registro.ComandoId);
            if (comando is null)
            {
                mensagens.Add($"Sem comando para reverter '{registro.AcaoId}' ({registro.ComandoId}).");
                continue;
            }

            await comando.ReverterAsync(registro, cancellationToken).ConfigureAwait(false);
            revertidas.Add(registro with { Revertido = true });
        }

        revertidas.Reverse();
        return new ResultadoCategoria
        {
            Categoria = categoria,
            Situacao = situacao,
            Alteracoes = revertidas,
            Mensagens = mensagens,
        };
    }
}
