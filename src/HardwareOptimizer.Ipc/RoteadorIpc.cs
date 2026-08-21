using System.Diagnostics;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Agent.Security;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Agent.Services;
using HardwareOptimizer.Agent.Smart;
using HardwareOptimizer.Agent.Startup;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Agent.Cleanup;
using HardwareOptimizer.Agent.Features;
using HardwareOptimizer.Agent.VisualEffects;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Cerebro.Visao;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Reporting;
using HardwareOptimizer.Features.Atualizacao;
using HardwareOptimizer.Features.Drivers;
using HardwareOptimizer.Features.Licensing;
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
    private readonly IServicoLicenca? _licenca;

    public RoteadorIpc(
        CatalogoAcoes? catalogo = null,
        IColetorInventario? coletor = null,
        ServicoSensores? sensores = null,
        ICerebro? cerebro = null,
        ILogger? logger = null,
        IServicoLicenca? licenca = null)
    {
        _catalogo = catalogo ?? CatalogoPadrao.Criar();
        _coletor = coletor ?? new ColetorInventario();
        _sensores = sensores ?? new ServicoSensores();
        _cerebro = cerebro ?? new CerebroLocal();
        _log = logger ?? NullLogger.Instance;
        _licenca = licenca;
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
                    ? await ToggleStartupAsync(requisicao, ativar: false, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "obtersaudediscos" => OperatingSystem.IsWindows()
                    ? ObterSaudeDiscosWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "varrerdrivers" => OperatingSystem.IsWindows()
                    ? await VarrerDriversWindowsAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "aprovaratualizacaodriver" => OperatingSystem.IsWindows()
                    ? await AprovarAtualizacaoDriverAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "reverteratualizacaodriver" => OperatingSystem.IsWindows()
                    ? await ReverterAtualizacaoDriverAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "desinstalarprogramas" => OperatingSystem.IsWindows()
                    ? await DesinstalarProgramasAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "ativarstartup" => OperatingSystem.IsWindows()
                    ? await ToggleStartupAsync(requisicao, ativar: true, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "obterservicos" => OperatingSystem.IsWindows()
                    ? await ObterServicosAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "iniciarservico" => OperatingSystem.IsWindows()
                    ? await ToggleServicoAsync(requisicao, iniciar: true, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "pararservico" => OperatingSystem.IsWindows()
                    ? await ToggleServicoAsync(requisicao, iniciar: false, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "alterarmododeinicio" => OperatingSystem.IsWindows()
                    ? await AlterarModoInicioAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "chat_upgrade" => await ChatUpgradeAsync(requisicao, cancellationToken).ConfigureAwait(false),
                "analise_upgrade" => await AnaliseInicialUpgradeAsync(requisicao, cancellationToken).ConfigureAwait(false),
                "chat_bios" => await ChatBiosAsync(requisicao, cancellationToken).ConfigureAwait(false),
                "exportarbackupdrivers" => OperatingSystem.IsWindows()
                    ? await ExportarBackupDriversAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "analisarbiosfoto" => await AnalisarBiosFotoAsync(requisicao, cancellationToken).ConfigureAwait(false),
                "obterstatuslicenca" => ObterStatusLicenca(requisicao),
                "obterefeitosvisuais" => OperatingSystem.IsWindows()
                    ? ObterEfeitosVisuaisWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "alterarefeito" => OperatingSystem.IsWindows()
                    ? AplicarEfeitoVisualWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "escanearlimpeza" => OperatingSystem.IsWindows()
                    ? EscanearLimpezaWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "executarlimpeza" => OperatingSystem.IsWindows()
                    ? ExecutarLimpezaWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "obterfeatures" => OperatingSystem.IsWindows()
                    ? ObterFeaturesWindows(requisicao)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "habilitarfeature" => OperatingSystem.IsWindows()
                    ? await HabilitarFeatureAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                "desabilitarfeature" => OperatingSystem.IsWindows()
                    ? await DesabilitarFeatureAsync(requisicao, cancellationToken).ConfigureAwait(false)
                    : RespostaIpc.Falha(requisicao.Id, "Requer Windows."),
                _ => RespostaIpc.Falha(requisicao.Id, $"Método desconhecido: {requisicao.Metodo}"),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            _log.LogError(ex, "IPC: falha no método '{Metodo}'.", requisicao.Metodo);
            return RespostaIpc.Falha(requisicao.Id, ex.Message);
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> ObterServicosAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var coletor = new ColetorServicos(NullLogger<ColetorServicos>.Instance);
        var servicos = await coletor.ColetarAsync(ct).ConfigureAwait(false);
        return RespostaIpc.Ok(req.Id, servicos);
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> ToggleServicoAsync(RequisicaoIpc req, bool iniciar, CancellationToken ct)
    {
        var nome = req.Parametros is { } p
            && p.TryGetProperty("nome", out var n)
            && n.ValueKind == JsonValueKind.String
                ? n.GetString() : null;

        if (string.IsNullOrEmpty(nome))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'nome' obrigatório.");

        // Trava de segurança: bloqueia operação em serviços críticos do SO
        var svcDummy = new ServicoWindows { Nome = nome, Descricao = nome, Status = "Unknown" };
        if (!ListaNegraServicos.EhSeguro(svcDummy))
            return RespostaIpc.Falha(req.Id, $"Operação bloqueada: '{nome}' é um serviço crítico do sistema.");

        var nomePs = nome.Replace("'", "''");
        var comando = iniciar
            ? $"Start-Service -Name '{nomePs}' -ErrorAction Stop"
            : $"Stop-Service  -Name '{nomePs}' -Force -ErrorAction Stop";

        var script  = $"try {{ {comando} }} catch {{ Write-Error $_; exit 1 }}; exit 0";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb            = "runas",
            WindowStyle     = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return proc.ExitCode == 0
                ? RespostaIpc.Ok(req.Id, true)
                : RespostaIpc.Falha(req.Id, $"Falha ao {(iniciar ? "iniciar" : "parar")} '{nome}' (código {proc.ExitCode}).");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return RespostaIpc.Falha(req.Id, "Operação cancelada (UAC negado).");
        }
        catch (Exception ex)
        {
            return RespostaIpc.Falha(req.Id, ex.Message);
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> AlterarModoInicioAsync(RequisicaoIpc req, CancellationToken ct)
    {
        if (req.Parametros is not { } p)
            return RespostaIpc.Falha(req.Id, "Parâmetros obrigatórios.");

        var nome = p.TryGetProperty("nome", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() : null;
        var modo = p.TryGetProperty("modo", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString() : null;

        if (string.IsNullOrEmpty(nome))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'nome' obrigatório.");
        if (string.IsNullOrEmpty(modo))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'modo' obrigatório.");

        // Trava de segurança: bloqueia alteração em serviços críticos do SO
        var svcDummy = new ServicoWindows { Nome = nome, Descricao = nome, Status = "Unknown" };
        if (!ListaNegraServicos.EhSeguro(svcDummy))
            return RespostaIpc.Falha(req.Id, $"Operação bloqueada: '{nome}' é um serviço crítico do sistema.");

        // Mapear PT-BR → PowerShell Set-Service -StartupType value
        var startupType = modo switch
        {
            "Automático"                       => "Automatic",
            "Automático (Atraso na Inicialização)" => "AutomaticDelayedStart",
            "Manual"                           => "Manual",
            "Desativado"                       => "Disabled",
            _ => null,
        };

        if (startupType is null)
            return RespostaIpc.Falha(req.Id, $"Modo de início desconhecido: '{modo}'.");

        var nomePs = nome.Replace("'", "''");
        var script  = $"try {{ Set-Service -Name '{nomePs}' -StartupType {startupType} -ErrorAction Stop }} catch {{ Write-Error $_; exit 1 }}; exit 0";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb            = "runas",
            WindowStyle     = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return proc.ExitCode == 0
                ? RespostaIpc.Ok(req.Id, true)
                : RespostaIpc.Falha(req.Id, $"Falha ao alterar modo de início de '{nome}' (código {proc.ExitCode}).");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return RespostaIpc.Falha(req.Id, "Operação cancelada (UAC negado).");
        }
        catch (Exception ex)
        {
            return RespostaIpc.Falha(req.Id, ex.Message);
        }
    }

    private RespostaIpc ObterStatusLicenca(RequisicaoIpc req)
    {
        var tipo = _licenca?.TipoAtual ?? TipoLicenca.Gratuita;
        var dto = new StatusLicencaDto
        {
            Tipo               = tipo.ToString(),
            ModuloUpgrade      = _licenca?.TemAcesso(FuncionalidadePremium.ModuloUpgrade)      ?? false,
            ContadorVidaUtil   = _licenca?.TemAcesso(FuncionalidadePremium.ContadorVidaUtil)   ?? false,
            GerenciadorDrivers = _licenca?.TemAcesso(FuncionalidadePremium.GerenciadorDrivers) ?? false,
            GuiaBiosIa         = _licenca?.TemAcesso(FuncionalidadePremium.GuiaBiosIa)         ?? false,
        };
        return RespostaIpc.Ok(req.Id, dto);
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

    /// <summary>
    /// Monta o <see cref="OrquestradorAtualizacao"/> — ponto único de varredura,
    /// backup, instalação e rollback de drivers. Nenhum outro método deste roteador
    /// deve tocar <see cref="AtualizadorDrivers"/>/<c>IRepositorioDriversWhql</c>
    /// diretamente (Boundaries §Always da spec-1-2-driver-scan-aprovacao-rollback).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static OrquestradorAtualizacao CriarOrquestradorAtualizacao()
    {
        var atualizador = new AtualizadorDrivers(
            new ColetorHwid(NullLogger<ColetorHwid>.Instance),
            new ProvedorFonteOficialDriver(new RepositorioWhqlEstatico()),
            NullLogger<AtualizadorDrivers>.Instance);
        return new OrquestradorAtualizacao(atualizador);
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> VarrerDriversWindowsAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var orquestrador = CriarOrquestradorAtualizacao();
        var drivers = await orquestrador.VarrerAsync(ct).ConfigureAwait(false);
        return RespostaIpc.Ok(req.Id, drivers);
    }

    /// <summary>
    /// Fluxo de aprovação: backup obrigatório dos drivers atuais ANTES de
    /// qualquer instalação (Boundaries §Always). Se o backup falhar, a
    /// instalação não é tentada. O caminho do backup é devolvido mesmo quando a
    /// instalação em si falha depois, para permanecer disponível como referência
    /// (I/O Matrix: "Instalação falha").
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> AprovarAtualizacaoDriverAsync(RequisicaoIpc req, CancellationToken ct)
    {
        if (req.Parametros is not { } p
            || !p.TryGetProperty("urlDownload", out var u)
            || u.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(u.GetString()))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'urlDownload' obrigatório.");

        var url = u.GetString()!;
        var orquestrador = CriarOrquestradorAtualizacao();

        // Backup obrigatório antes de qualquer instalação — sem backup bem-sucedido,
        // a instalação não prossegue (Boundaries §Always).
        var backup = await orquestrador.BackupAsync(ct).ConfigureAwait(false);
        if (!backup.Sucesso)
        {
            return RespostaIpc.Ok(req.Id, new ResultadoAprovacaoDriverDto
            {
                Sucesso = false,
                Erro = "Backup falhou: " + backup.Erro,
                CaminhoBackup = null,
            });
        }

        string ext;
        try { ext = Path.GetExtension(new Uri(url).LocalPath).ToLowerInvariant(); }
        catch
        {
            // A URL pode não ser um Uri válido nem um caminho de arquivo válido —
            // Path.GetExtension também lança para caracteres inválidos. Nunca deixa
            // escapar sem tratamento (o filtro de exceção de TratarAsync não cobre
            // ArgumentException/UriFormatException).
            try { ext = Path.GetExtension(url).ToLowerInvariant(); }
            catch
            {
                return RespostaIpc.Ok(req.Id, new ResultadoAprovacaoDriverDto
                {
                    Sucesso = false,
                    Erro = "URL de download inválida — não foi possível determinar o formato do arquivo.",
                    CaminhoBackup = backup.CaminhoBackup,
                });
            }
        }

        if (ext is not (".inf" or ".cab"))
        {
            return RespostaIpc.Ok(req.Id, new ResultadoAprovacaoDriverDto
            {
                Sucesso = false,
                Erro = $"Formato '{ext}' não suportado para instalação via pnputil.",
                CaminhoBackup = backup.CaminhoBackup,
            });
        }

        var pastaTemp = Path.Combine(Path.GetTempPath(), "OtimizeBuilder", "Drivers", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(pastaTemp);
        }
        catch (Exception ex)
        {
            return RespostaIpc.Ok(req.Id, new ResultadoAprovacaoDriverDto
            {
                Sucesso = false,
                Erro = $"Falha ao preparar pasta temporária: {ex.Message}",
                CaminhoBackup = backup.CaminhoBackup,
            });
        }

        string nomeArq;
        try { nomeArq = Path.GetFileName(new Uri(url).LocalPath); }
        catch { nomeArq = string.Empty; }
        if (string.IsNullOrEmpty(nomeArq)) nomeArq = $"driver{ext}";

        var destino = Path.Combine(pastaTemp, nomeArq);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var bytes = await http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(destino, bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return RespostaIpc.Ok(req.Id, new ResultadoAprovacaoDriverDto
            {
                Sucesso = false,
                Erro = $"Falha no download: {ex.Message}",
                CaminhoBackup = backup.CaminhoBackup,
            });
        }

        var instalacao = await orquestrador.InstalarAsync(destino, ct).ConfigureAwait(false);
        return RespostaIpc.Ok(req.Id, new ResultadoAprovacaoDriverDto
        {
            Sucesso = instalacao.Sucesso,
            Erro = instalacao.Sucesso ? null : instalacao.MensagemErro,
            CaminhoBackup = backup.CaminhoBackup,
        });
    }

    /// <summary>
    /// Rollback acionado pelo usuário: reinstala a partir de um backup exportado
    /// anteriormente. Nunca automático/silencioso (Boundaries §Never).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> ReverterAtualizacaoDriverAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var caminho = LerParamString(req.Parametros, "caminhoBackup");
        if (string.IsNullOrWhiteSpace(caminho))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'caminhoBackup' obrigatório.");

        var orquestrador = CriarOrquestradorAtualizacao();
        var resultado = await orquestrador.ReverterAsync(caminho, ct).ConfigureAwait(false);
        return resultado.Sucesso
            ? RespostaIpc.Ok(req.Id, true)
            : RespostaIpc.Falha(req.Id, resultado.MensagemErro);
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

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> ExportarBackupDriversAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OtimizeBuilder", "DriverBackups",
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

        Directory.CreateDirectory(pasta);

        var psi = new ProcessStartInfo
        {
            FileName               = "pnputil.exe",
            Arguments              = $"/export-driver * \"{pasta}\"",
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        try
        {
            using var proc = Process.Start(psi)!;
            await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            return proc.ExitCode == 0
                ? RespostaIpc.Ok(req.Id, pasta)
                : RespostaIpc.Falha(req.Id, $"pnputil saiu com código {proc.ExitCode}.");
        }
        catch (Exception ex)
        {
            return RespostaIpc.Falha(req.Id, ex.Message);
        }
    }

    private async Task<RespostaIpc> AnalisarBiosFotoAsync(RequisicaoIpc req, CancellationToken ct)
    {
        if (req.Parametros is not { } p)
            return RespostaIpc.Falha(req.Id, "Parâmetros obrigatórios.");

        var b64 = p.TryGetProperty("imagemBase64", out var b) && b.ValueKind == JsonValueKind.String
            ? b.GetString() : null;
        var mt = p.TryGetProperty("mediaType", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString() ?? "image/png" : "image/png";

        if (string.IsNullOrWhiteSpace(b64))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'imagemBase64' obrigatório.");

        var imagem = new ImagemEntrada { Base64 = b64, MediaType = mt };
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var modelo = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-6";
        var cliente = new ClienteVisaoAnthropic(modelo, apiKey);
        var modulo = new ModuloVisao(cliente, _log);

        try
        {
            var leitura = await modulo.InterpretarAsync(imagem, CasoUsoVisao.LerVersaoBios, ct).ConfigureAwait(false);
            return RespostaIpc.Ok(req.Id, FormatarLeituraBios(leitura));
        }
        catch (Exception ex)
        {
            return RespostaIpc.Falha(req.Id, $"Erro na análise da imagem: {ex.Message}");
        }
    }

    private static string FormatarLeituraBios(LeituraVisual leitura)
    {
        if (leitura.TipoTela == TipoTela.Desconhecida || leitura.Confianca == NivelConfianca.Baixa)
            return leitura.ProximoPasso
                ?? "Não foi possível identificar a tela de BIOS. Envie uma foto mais nítida.";

        var sb = new StringBuilder();
        sb.AppendLine("Imagem identificada: BIOS/UEFI");

        if (leitura.Campo("fabricante") is { Length: > 0 } fab)
            sb.AppendLine($"Fabricante: {fab}");
        if (leitura.Campo("modelo") is { Length: > 0 } mod)
            sb.AppendLine($"Modelo: {mod}");
        if (leitura.Campo("versao") is { Length: > 0 } ver)
            sb.AppendLine($"Versão BIOS: {ver}");

        if (leitura.ProximoPasso is { Length: > 0 } prox)
        {
            sb.AppendLine();
            sb.Append($"→ {prox}");
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<RespostaIpc> ChatBiosAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var pergunta = req.Parametros is { } p
            && p.TryGetProperty("pergunta", out var q) && q.ValueKind == JsonValueKind.String
            ? q.GetString() : null;

        if (string.IsNullOrWhiteSpace(pergunta))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'pergunta' obrigatório.");

        var inventario = await _coletor.ColetarAsync(ct).ConfigureAwait(false);

        var ram = inventario.Memoria.FirstOrDefault();
        var velocidadeRam = ram?.VelocidadeMhz > 0 ? $"{ram.VelocidadeMhz} MHz" : "desconhecida";
        var tipoRam = ram?.VelocidadeMhz >= 4800 ? "DDR5" : "DDR4";

        var systemPrompt = $"""
            Você é um especialista técnico em BIOS de placas-mãe para Windows.

            Configuração do sistema do usuário:
            - Placa-mãe: {inventario.Placa.Fabricante} {inventario.Placa.Modelo}
            - CPU: {inventario.Cpu.Nome}
            - RAM: {inventario.Memoria.Count}x módulo(s) {tipoRam} @ {velocidadeRam}
            - BIOS versão: {inventario.Placa.VersaoBios ?? "desconhecida"}

            Responda em português, de forma direta e técnica.
            Indique o caminho de menu específico para o fabricante quando relevante
            (ex: ASUS → AI Tweaker › X.M.P., MSI → OC › XMP).
            Máximo 200 palavras.
            """;

        var cliente = ObterClienteLlm();
        var resposta = await cliente.ResponderAsync(systemPrompt, pergunta, ct).ConfigureAwait(false);
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
    private static async Task<RespostaIpc> ToggleStartupAsync(RequisicaoIpc req, bool ativar, CancellationToken ct)
    {
        var nome = req.Parametros is { } p
            && p.TryGetProperty("nome", out var n)
            && n.ValueKind == JsonValueKind.String
                ? n.GetString() : null;

        if (string.IsNullOrEmpty(nome))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'nome' obrigatório.");

        // Localiza a entrada para saber hive (HKCU/HKLM) e se é pasta de startup.
        var entrada = new VerificadorInicializacao(NullLogger<VerificadorInicializacao>.Instance)
            .Varrer()
            .FirstOrDefault(e => string.Equals(e.Nome, nome, StringComparison.OrdinalIgnoreCase));

        if (entrada is null)
            return RespostaIpc.Falha(req.Id, $"Entrada '{nome}' não encontrada no registro.");

        // Entradas de pasta de startup: renomear arquivo (sem script de registro).
        if (entrada.Origem == OrigemInicializacao.PastaStartup)
        {
            var gerenciador = new GerenciadorInicializacao(NullLogger<GerenciadorInicializacao>.Instance);
            var r = ativar ? gerenciador.Ativar(entrada, "") : gerenciador.Desativar(entrada);
            return r.Sucesso ? RespostaIpc.Ok(req.Id, true) : RespostaIpc.Falha(req.Id, r.MensagemErro);
        }

        // Entradas de registro: usa PowerShell com -EncodedCommand para garantir execução nativa.
        var hive = entrada.Origem == OrigemInicializacao.RegistroUsuario ? "HKCU" : "HKLM";
        var statusByte = ativar ? 2 : 3;
        var nomePs = nome.Replace("'", "''"); // escapa aspas simples para PS

        var script = $@"
$hive = '{hive}:'
$path = Join-Path $hive 'SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
if (-not (Test-Path $path)) {{ New-Item -Path $path -Force | Out-Null }}
$bytes = [byte[]]@({statusByte},0,0,0,0,0,0,0,0,0,0,0)
Set-ItemProperty -Path $path -Name '{nomePs}' -Value $bytes -Type Binary -Force
Write-Output 'OK'
";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        try
        {
            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            return proc.ExitCode == 0 && stdout.Contains("OK")
                ? RespostaIpc.Ok(req.Id, true)
                : RespostaIpc.Falha(req.Id, string.IsNullOrWhiteSpace(stderr)
                    ? $"Script saiu com código {proc.ExitCode}."
                    : stderr.Trim());
        }
        catch (Exception ex)
        {
            return RespostaIpc.Falha(req.Id, ex.Message);
        }
    }

    [SupportedOSPlatform("windows")]
    private static RespostaIpc ObterEfeitosVisuaisWindows(RequisicaoIpc req)
    {
        var efeitos = GerenciadorEfeitosVisuais.ObterTodos();
        return RespostaIpc.Ok(req.Id, efeitos);
    }

    [SupportedOSPlatform("windows")]
    private static RespostaIpc AplicarEfeitoVisualWindows(RequisicaoIpc req)
    {
        if (req.Parametros is not { } p
            || !p.TryGetProperty("id",    out var idEl)    || idEl.ValueKind    != JsonValueKind.String
            || !p.TryGetProperty("ativo", out var ativoEl) || ativoEl.ValueKind != JsonValueKind.True
                && ativoEl.ValueKind != JsonValueKind.False)
            return RespostaIpc.Falha(req.Id, "Parâmetros 'id' e 'ativo' obrigatórios.");

        var id    = idEl.GetString()!;
        var ativo = ativoEl.GetBoolean();
        var ok    = GerenciadorEfeitosVisuais.Aplicar(id, ativo);
        return ok
            ? RespostaIpc.Ok(req.Id, true)
            : RespostaIpc.Falha(req.Id, $"Falha ao aplicar efeito '{id}'.");
    }

    [SupportedOSPlatform("windows")]
    private static RespostaIpc ObterFeaturesWindows(RequisicaoIpc req)
    {
        var features = GerenciadorFeaturesWindows.ObterInfos();
        return RespostaIpc.Ok(req.Id, features);
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> HabilitarFeatureAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var nome = LerParamString(req.Parametros, "nome");
        if (string.IsNullOrEmpty(nome))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'nome' obrigatório.");

        var (ok, erro) = await GerenciadorFeaturesWindows.HabilitarAsync(nome, ct).ConfigureAwait(false);
        return ok ? RespostaIpc.Ok(req.Id, true) : RespostaIpc.Falha(req.Id, erro ?? "Falha desconhecida.");
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RespostaIpc> DesabilitarFeatureAsync(RequisicaoIpc req, CancellationToken ct)
    {
        var nome = LerParamString(req.Parametros, "nome");
        if (string.IsNullOrEmpty(nome))
            return RespostaIpc.Falha(req.Id, "Parâmetro 'nome' obrigatório.");

        var (ok, erro) = await GerenciadorFeaturesWindows.DesabilitarAsync(nome, ct).ConfigureAwait(false);
        return ok ? RespostaIpc.Ok(req.Id, true) : RespostaIpc.Falha(req.Id, erro ?? "Falha desconhecida.");
    }

    private static string? LerParamString(JsonElement? parametros, string chave) =>
        parametros is { } p && p.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    [SupportedOSPlatform("windows")]
    private static RespostaIpc EscanearLimpezaWindows(RequisicaoIpc req)
    {
        var categorias = GerenciadorLimpeza.Escanear();
        return RespostaIpc.Ok(req.Id, categorias);
    }

    [SupportedOSPlatform("windows")]
    private static RespostaIpc ExecutarLimpezaWindows(RequisicaoIpc req)
    {
        if (req.Parametros is not { } p
            || !p.TryGetProperty("ids", out var idsEl)
            || idsEl.ValueKind != JsonValueKind.Array)
            return RespostaIpc.Falha(req.Id, "Parâmetro 'ids' (array) obrigatório.");

        var ids = idsEl.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();

        var resultado = GerenciadorLimpeza.Limpar(ids);
        return RespostaIpc.Ok(req.Id, resultado);
    }
}
