using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Ipc;

/// <summary>
/// Dispatcher do protocolo IPC: traduz uma <see cref="RequisicaoIpc"/> em chamada
/// aos módulos do agente e devolve uma <see cref="RespostaIpc"/>. Lógica pura de
/// roteamento (sem transporte), portanto totalmente testável.
/// </summary>
public sealed class RoteadorIpc : IRoteadorIpc
{
    private readonly CatalogoAcoes _catalogo;
    private readonly IColetorInventario _coletor;
    private readonly ServicoSensores _sensores;
    private readonly ICerebro _cerebro;
    private readonly ILogger _log;

    public RoteadorIpc(
        CatalogoAcoes? catalogo = null,
        IColetorInventario? coletor = null,
        ServicoSensores? sensores = null,
        ICerebro? cerebro = null,
        ILogger? logger = null)
    {
        _catalogo = catalogo ?? CatalogoPadrao.Criar();
        _coletor = coletor ?? new ColetorInventario();
        _sensores = sensores ?? new ServicoSensores();
        _cerebro = cerebro ?? new CerebroLocal();
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        _log.LogInformation("IPC: método '{Metodo}' (id {Id}).", requisicao.Metodo, requisicao.Id);

        try
        {
            return requisicao.Metodo.ToLowerInvariant() switch
            {
                "ping" => RespostaIpc.Ok(requisicao.Id, "pong"),
                "coletar" => RespostaIpc.Ok(requisicao.Id, await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false)),
                "sensores" => RespostaIpc.Ok(requisicao.Id, await _sensores.LerAsync(cancellationToken).ConfigureAwait(false)),
                "catalogo" => RespostaIpc.Ok(requisicao.Id, ListarCatalogo()),
                "proposta" => RespostaIpc.Ok(requisicao.Id, await ProporAsync(cancellationToken).ConfigureAwait(false)),
                "relatorio" => RespostaIpc.Ok(requisicao.Id, await RelatorioAsync(cancellationToken).ConfigureAwait(false)),
                "aprovar" => await AprovarAsync(requisicao, cancellationToken).ConfigureAwait(false),
                _ => RespostaIpc.Falha(requisicao.Id, $"Método desconhecido: {requisicao.Metodo}"),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            _log.LogError(ex, "IPC: falha no método '{Metodo}'.", requisicao.Metodo);
            return RespostaIpc.Falha(requisicao.Id, ex.Message);
        }
    }

    private IReadOnlyList<AcaoResumoDto> ListarCatalogo() =>
        _catalogo.Todas
            .OrderBy(a => a.Categoria)
            .ThenBy(a => a.Id, StringComparer.Ordinal)
            .Select(AcaoResumoDto.De)
            .ToList();

    private async Task<MatrizDecisao> ProporAsync(CancellationToken cancellationToken)
    {
        var inventario = await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false);
        var sanitizado = new Sanitizador().Sanitizar(inventario).InventarioSeguro;
        return await _cerebro.ProporAsync(sanitizado, _catalogo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RelatorioExecutivo> RelatorioAsync(CancellationToken cancellationToken)
    {
        var inventario = await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false);
        return new GeradorRelatorio().Gerar(
            inventario,
            Array.Empty<ResultadoValidacao>(),
            Array.Empty<AlteracaoResumo>(),
            new HashSet<Dominio>());
    }

    /// <summary>
    /// Fluxo de aprovação explícita por ação: a UI envia os IDs aprovados; o
    /// agente monta o perfil seguro, faz backup obrigatório e executa por
    /// categoria com validação (e rollback automático em regressão).
    /// </summary>
    private async Task<RespostaIpc> AprovarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken)
    {
        var acoes = LerAcoes(requisicao.Parametros);
        if (acoes.Count == 0)
        {
            return RespostaIpc.Falha(requisicao.Id, "Nenhuma ação aprovada (parâmetro 'acoes' vazio).");
        }

        var construcao = new ConstrutorPerfil(_catalogo).CriarPerfilSeguro(LerNomePerfil(requisicao.Parametros), acoes);
        if (!construcao.Sucesso)
        {
            return RespostaIpc.Falha(requisicao.Id, "Perfil inválido: " + string.Join(" | ", construcao.Bloqueios));
        }

        var inventario = await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false);
        var backup = await new ServicoBackup().CriarBackupAsync(inventario, cancellationToken).ConfigureAwait(false);

        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            _catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel()));

        var relatorio = await executor
            .AplicarPerfilAsync(construcao.Perfil!, new ContextoExecucao { BackupConfirmado = backup.Sucesso }, cancellationToken)
            .ConfigureAwait(false);

        return RespostaIpc.Ok(requisicao.Id, relatorio);
    }

    private static IReadOnlyList<string> LerAcoes(JsonElement? parametros)
    {
        var acoes = new List<string>();
        if (parametros is { } p && p.ValueKind == JsonValueKind.Object
            && p.TryGetProperty("acoes", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var elemento in arr.EnumerateArray())
            {
                if (elemento.ValueKind == JsonValueKind.String && elemento.GetString() is { } id)
                {
                    acoes.Add(id);
                }
            }
        }

        return acoes;
    }

    private static string LerNomePerfil(JsonElement? parametros) =>
        parametros is { } p && p.ValueKind == JsonValueKind.Object
        && p.TryGetProperty("nomePerfil", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!
            : "perfil-ipc";
}
