using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Agent.Smart;
using HardwareOptimizer.Agent.Startup;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Reporting;
using HardwareOptimizer.Features.LifeCounter;
using HardwareOptimizer.Features.Upgrade.Agente;
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
                "aprovar" or "aplicar" => await AprovarAsync(requisicao, cancellationToken).ConfigureAwait(false),
                "obterentradasstartup" => OperatingSystem.IsWindows()
                    ? ObterEntradasStartupWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "desativarstartup" => OperatingSystem.IsWindows()
                    ? DesativarStartupWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "obtersaudediscos" => OperatingSystem.IsWindows()
                    ? ObterSaudeDiscosWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "obterdrivers" => OperatingSystem.IsWindows()
                    ? ObterDriversWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "desinstalarprogramas" => OperatingSystem.IsWindows()
                    ? await DesinstalarProgramasAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "ativarstartup" => OperatingSystem.IsWindows()
                    ? AtivarStartupWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "chat_upgrade" => await ChatUpgradeAsync(requisicao, cancellationToken).ConfigureAwait(false),
                "analise_upgrade" => await AnaliseInicialUpgradeAsync(requisicao, cancellationToken).ConfigureAwait(false),
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

        var estado = EstadoSistemaWindows.Selecionar(_log);
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

    [SupportedOSPlatform("windows")]
    private RespostaIpc ObterSaudeDiscosWindows(RequisicaoIpc req)
    {
        var leitor = new LeitorSmart(NullLogger<LeitorSmart>.Instance);
        var calc = new CalculadoraVidaUtil(leitor, NullLogger<CalculadoraVidaUtil>.Instance);
        var resultado = calc.Calcular();
        return RespostaIpc.Ok(req.Id, resultado);
    }

    [SupportedOSPlatform("windows")]
    private RespostaIpc ObterDriversWindows(RequisicaoIpc req)
    {
        var coletor = new ColetorHwid(NullLogger<ColetorHwid>.Instance);
        var drivers = coletor.Coletar();
        return RespostaIpc.Ok(req.Id, drivers);
    }

    [SupportedOSPlatform("windows")]
    private RespostaIpc ObterEntradasStartupWindows(RequisicaoIpc req)
    {
        var verificador = new VerificadorInicializacao(NullLogger<VerificadorInicializacao>.Instance);
        var entradas = verificador.Varrer();
        return RespostaIpc.Ok(req.Id, entradas);
    }

    private async Task<RespostaIpc> ChatUpgradeAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var mensagem = req.Parametros is { } p
            && p.TryGetProperty("mensagem", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString() : null;

        if (string.IsNullOrWhiteSpace(mensagem))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'mensagem' obrigatório.");

        var historico = LerHistoricoChat(req.Parametros);
        var inventario = await _coletor.ColetarAsync(ct).ConfigureAwait(false);
        var agente = new AgenteUpgrade(ObterClienteLlm(), _log);
        var resposta = await agente.ResponderAsync(inventario, mensagem, historico, ct).ConfigureAwait(false);
        return RespostaIpc.Ok(req.Id, resposta);
    }

    private async Task<RespostaIpc> AnaliseInicialUpgradeAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var inventario = await _coletor.ColetarAsync(ct).ConfigureAwait(false);
        var agente = new AgenteUpgrade(ObterClienteLlm(), _log);
        var resposta = await agente.AnalisarInicialAsync(inventario, ct).ConfigureAwait(false);
        return RespostaIpc.Ok(req.Id, resposta);
    }

    private IClienteLlm ObterClienteLlm()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var modelo = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-6";
        return new ClienteLlmAnthropic(modelo, apiKey);
    }

    private static IReadOnlyList<MensagemChat> LerHistoricoChat(JsonElement? parametros)
    {
        var lista = new List<MensagemChat>();
        if (parametros is not { } p || p.ValueKind != JsonValueKind.Object
            || !p.TryGetProperty("historico", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return lista;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var role = item.TryGetProperty("role", out var r) ? r.GetString() : null;
            var conteudo = item.TryGetProperty("conteudo", out var c) ? c.GetString() : null;
            if (role is not null && conteudo is not null)
                lista.Add(new MensagemChat { Role = role, Conteudo = conteudo });
        }

        return lista;
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> DesinstalarProgramasAsync(RequisicaoIpc req, CancellationToken ct)
    {
        if (req.Parametros is not { } p
            || !p.TryGetProperty("programas", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            return RespostaIpc.Falha(req.Id, "Parâmetro 'programas' obrigatório.");

        int iniciados = 0;
        var erros = new List<string>();

        foreach (var item in arr.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            string? cmdStr = null;
            if (item.TryGetProperty("quietUninstallString", out var q)
                && q.ValueKind == JsonValueKind.String
                && q.GetString() is { Length: > 0 } qs)
                cmdStr = qs;
            else if (item.TryGetProperty("uninstallString", out var u)
                && u.ValueKind == JsonValueKind.String)
                cmdStr = u.GetString();

            if (string.IsNullOrWhiteSpace(cmdStr)) continue;

            var nome = item.TryGetProperty("nome", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? "?" : "?";
            try
            {
                Process.Start(ParseUninstallCommand(cmdStr));
                iniciados++;
                await Task.Delay(600, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                erros.Add($"{nome}: {ex.Message}");
            }
        }

        return erros.Count == 0
            ? RespostaIpc.Ok(req.Id, iniciados)
            : RespostaIpc.Falha(req.Id, $"Iniciados: {iniciados}. Falhas: {string.Join("; ", erros)}");
    }

    private static ProcessStartInfo ParseUninstallCommand(string cmd)
    {
        cmd = cmd.Trim();

        if (cmd.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase))
        {
            var spIdx = cmd.IndexOf(' ');
            var args = spIdx > 0 ? cmd[(spIdx + 1)..].Trim() : "";
            args = args.Replace("/I{", "/X{", StringComparison.OrdinalIgnoreCase);
            return new ProcessStartInfo { FileName = "msiexec.exe", Arguments = args, UseShellExecute = true };
        }

        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            if (end > 0)
            {
                var exe = cmd[1..end];
                var args = cmd.Length > end + 1 ? cmd[(end + 1)..].Trim() : "";
                return new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true };
            }
        }

        var sp = cmd.IndexOf(' ');
        return sp > 0
            ? new ProcessStartInfo { FileName = cmd[..sp], Arguments = cmd[(sp + 1)..], UseShellExecute = true }
            : new ProcessStartInfo { FileName = cmd, UseShellExecute = true };
    }

    [SupportedOSPlatform("windows")]
    private RespostaIpc DesativarStartupWindows(RequisicaoIpc req)
    {
        var entrada = EncontrarEntradaStartup(req);
        if (entrada is null)
            return RespostaIpc.Falha(req.Id, "Entrada não encontrada ou parâmetro 'nome' ausente.");

        var gerenciador = new GerenciadorInicializacao(NullLogger<GerenciadorInicializacao>.Instance);
        var resultado = gerenciador.Desativar(entrada);
        return resultado.Sucesso
            ? RespostaIpc.Ok(req.Id, true)
            : RespostaIpc.Falha(req.Id, resultado.MensagemErro);
    }

    [SupportedOSPlatform("windows")]
    private RespostaIpc AtivarStartupWindows(RequisicaoIpc req)
    {
        var entrada = EncontrarEntradaStartup(req);
        if (entrada is null)
            return RespostaIpc.Falha(req.Id, "Entrada não encontrada ou parâmetro 'nome' ausente.");

        var gerenciador = new GerenciadorInicializacao(NullLogger<GerenciadorInicializacao>.Instance);
        var resultado = gerenciador.Ativar(entrada, string.Empty);
        return resultado.Sucesso
            ? RespostaIpc.Ok(req.Id, true)
            : RespostaIpc.Falha(req.Id, resultado.MensagemErro);
    }

    [SupportedOSPlatform("windows")]
    private static InicializacaoEntrada? EncontrarEntradaStartup(RequisicaoIpc req)
    {
        var nome = req.Parametros is { } p
            && p.TryGetProperty("nome", out var n)
            && n.ValueKind == JsonValueKind.String
                ? n.GetString() : null;

        if (string.IsNullOrEmpty(nome)) return null;

        return new VerificadorInicializacao(NullLogger<VerificadorInicializacao>.Instance)
            .Varrer()
            .FirstOrDefault(e => string.Equals(e.Nome, nome, StringComparison.OrdinalIgnoreCase));
    }
}
