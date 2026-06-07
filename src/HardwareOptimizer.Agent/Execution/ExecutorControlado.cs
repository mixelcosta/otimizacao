using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Aplica um perfil aprovado, uma categoria por vez, na ordem do documento.
/// Após cada categoria, valida a estabilidade; se reprovar (ou se uma
/// pré-condição falhar), reverte a categoria inteira pelo registro de
/// alterações. Executa somente comandos do registro — nada fora do catálogo.
/// Cada passo é registrado em log para diagnóstico do ponto exato de falha.
/// </summary>
public sealed class ExecutorControlado
{
    private readonly CatalogoAcoes _catalogo;
    private readonly RegistroComandos _comandos;
    private readonly IVerificadorPreCondicoes _preCondicoes;
    private readonly IValidadorCategoria _validadorCategoria;
    private readonly ILogger _log;

    public ExecutorControlado(
        CatalogoAcoes catalogo,
        RegistroComandos comandos,
        IVerificadorPreCondicoes preCondicoes,
        IValidadorCategoria validadorCategoria,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(comandos);
        ArgumentNullException.ThrowIfNull(preCondicoes);
        ArgumentNullException.ThrowIfNull(validadorCategoria);

        _catalogo = catalogo;
        _comandos = comandos;
        _preCondicoes = preCondicoes;
        _validadorCategoria = validadorCategoria;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<RelatorioExecucao> AplicarPerfilAsync(
        Perfil perfil, ContextoExecucao contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        // Perfil customizado só pode ser aplicado após consentimento registrado.
        if (!perfil.PodeAplicar)
        {
            _log.LogWarning(
                "Execução bloqueada: perfil customizado '{Perfil}' sem consentimento registrado.", perfil.Nome);
            return new RelatorioExecucao
            {
                Sucesso = false,
                PerfilNome = perfil.Nome,
                Mensagens = new[] { "Perfil customizado sem consentimento registrado: execução bloqueada." },
            };
        }

        _log.LogInformation(
            "Iniciando execução do perfil '{Perfil}' ({Qtd} ações, backup={Backup}).",
            perfil.Nome, perfil.Selecoes.Count, contexto.BackupConfirmado);

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

        _log.LogInformation(
            "Execução do perfil '{Perfil}' finalizada. Sucesso geral={Sucesso}.", perfil.Nome, sucessoGeral);

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
        _log.LogInformation("Categoria {Categoria}: aplicando.", categoria);

        foreach (var (selecao, acao) in itens)
        {
            var pre = _preCondicoes.Verificar(acao!, selecao.Parametros, contexto);
            if (pre.Falha)
            {
                _log.LogWarning(
                    "Categoria {Categoria} BLOQUEADA na ação '{Acao}': {Motivo}",
                    categoria, acao!.Id, pre.MensagemErro);
                mensagens.AddRange(pre.Erros);
                return await ReverterCategoriaAsync(
                    categoria, aplicadas, SituacaoCategoria.Bloqueada, mensagens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var comando = _comandos.Obter(acao!.ComandoInternoId);
            if (comando is null)
            {
                _log.LogError(
                    "Categoria {Categoria}: comando interno '{Comando}' da ação '{Acao}' não está registrado.",
                    categoria, acao.ComandoInternoId, acao.Id);
                mensagens.Add($"Ação '{acao.Id}': comando interno '{acao.ComandoInternoId}' não registrado.");
                return await ReverterCategoriaAsync(
                    categoria, aplicadas, SituacaoCategoria.Bloqueada, mensagens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var registro = await comando
                .AplicarAsync(acao.Id, categoria, selecao.Parametros, cancellationToken)
                .ConfigureAwait(false);
            aplicadas.Add(registro);
            _log.LogDebug(
                "Ação '{Acao}' aplicada: {Alvo} '{Antes}' -> '{Depois}'.",
                acao.Id, registro.Alvo, registro.ValorAnterior ?? "(não definido)", registro.ValorNovo);
        }

        // Validação por categoria (runner de testes). Reprovou -> reverte tudo.
        var validacao = await _validadorCategoria
            .ValidarAsync(categoria, aplicadas, cancellationToken)
            .ConfigureAwait(false);

        if (validacao.Regressao)
        {
            _log.LogWarning(
                "Categoria {Categoria}: REGRESSÃO detectada ({Ferramenta}); revertendo {Qtd} alteração(ões).",
                categoria, validacao.Ferramenta, aplicadas.Count);
            mensagens.Add($"Regressão detectada na categoria {categoria}: revertendo.");
            var revertida = await ReverterCategoriaAsync(
                categoria, aplicadas, SituacaoCategoria.Revertida, mensagens, cancellationToken)
                .ConfigureAwait(false);
            return revertida with { Validacao = validacao };
        }

        _log.LogInformation(
            "Categoria {Categoria}: APLICADA com {Qtd} alteração(ões).", categoria, aplicadas.Count);
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
        if (aplicadas.Count > 0)
        {
            _log.LogWarning(
                "Categoria {Categoria}: revertendo {Qtd} alteração(ões) (situação {Situacao}).",
                categoria, aplicadas.Count, situacao);
        }

        var revertidas = new List<RegistroAlteracao>(aplicadas.Count);

        // Reverte na ordem inversa da aplicação.
        for (var i = aplicadas.Count - 1; i >= 0; i--)
        {
            var registro = aplicadas[i];
            var comando = _comandos.Obter(registro.ComandoId);
            if (comando is null)
            {
                _log.LogError(
                    "Sem comando para reverter '{Acao}' ({Comando}); estado pode ficar inconsistente.",
                    registro.AcaoId, registro.ComandoId);
                mensagens.Add($"Sem comando para reverter '{registro.AcaoId}' ({registro.ComandoId}).");
                continue;
            }

            await comando.ReverterAsync(registro, cancellationToken).ConfigureAwait(false);
            _log.LogDebug("Revertido: {Alvo} -> '{Anterior}'.", registro.Alvo, registro.ValorAnterior ?? "(removido)");
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
