using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Bios;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Ipc;
using HardwareOptimizer.Cerebro.Visao;
using HardwareOptimizer.Cli;
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal static class Program
{
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private static async Task<int> Main(string[] args)
    {
        var comando = args.Length > 0 ? args[0].ToLowerInvariant() : "ajuda";

        // Log persistente em arquivo, para análise posterior do ponto exato de falha.
        var caminhoLog = Path.Combine(
            AppContext.BaseDirectory, "data", "logs", $"otimizador-{DateTime.Now:yyyyMMdd}.log");
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new ArquivoLoggerProvider(caminhoLog, LogLevel.Debug));
        });
        _loggerFactory = loggerFactory;

        var log = loggerFactory.CreateLogger("Program");
        log.LogInformation("=== Início: comando '{Comando}' ===", comando);
        // Caminho do log vai para stderr para não poluir a saída JSON em stdout.
        Console.Error.WriteLine($"[log] Registro do processo em: {caminhoLog}");

        try
        {
            switch (comando)
            {
                case "coletar":
                    await ComandoColetar();
                    return 0;
                case "sanitizar":
                    await ComandoSanitizar();
                    return 0;
                case "catalogo":
                    ComandoCatalogo();
                    return 0;
                case "relatorio":
                    await ComandoRelatorio();
                    return 0;
                case "sensores":
                    await ComandoSensores();
                    return 0;
                case "servir":
                    await ComandoServir(args);
                    return 0;
                case "ipc-demo":
                    await ComandoIpcDemo();
                    return 0;
                case "bios":
                    await ComandoBios();
                    return 0;
                case "proposta":
                    await ComandoProposta();
                    return 0;
                case "visao":
                    await ComandoVisao(args);
                    return 0;
                case "demo":
                    await ComandoDemo();
                    return 0;
                default:
                    ImprimirAjuda();
                    return 0;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            log.LogError(ex, "Falha ao executar o comando '{Comando}'.", comando);
            Console.Error.WriteLine("Erro: " + ex.Message);
            return 1;
        }
        finally
        {
            log.LogInformation("=== Fim: comando '{Comando}' ===", comando);
        }
    }

    private static ILogger Log<T>() => _loggerFactory.CreateLogger<T>();

    private static void ImprimirAjuda()
    {
        Apresentacao.Linha("Agente de Otimização e Confiabilidade de Hardware — CLI (MVP)");
        Apresentacao.Linha();
        Apresentacao.Linha("Uso: hwopt <comando>");
        Apresentacao.Linha();
        Apresentacao.Linha("Comandos:");
        Apresentacao.Linha("  coletar     Coleta o inventário (read-only) e imprime em JSON.");
        Apresentacao.Linha("  sanitizar   Coleta e mostra a versão segura para nuvem + relatório de privacidade.");
        Apresentacao.Linha("  catalogo    Lista o catálogo de ações whitelisted e seus limites.");
        Apresentacao.Linha("  relatorio   Gera o relatório executivo e a nota 0-100 do equipamento.");
        Apresentacao.Linha("  sensores    Lê os sensores (temperatura, clock, voltagem, fan, consumo) em tempo real.");
        Apresentacao.Linha("  servir      Hospeda o servidor IPC (named pipe) para a UI. Ctrl+C encerra.");
        Apresentacao.Linha("  ipc-demo    Demonstra o IPC (servidor + cliente no mesmo processo).");
        Apresentacao.Linha("  bios        Identifica a BIOS, verifica com o fabricante e gera o guia (não aplica).");
        Apresentacao.Linha("  proposta    Cérebro propõe a matriz de decisão a partir do inventário sanitizado.");
        Apresentacao.Linha("  visao <img> Interpreta uma foto (BIOS/etiqueta/erro/benchmark) e cruza com o inventário.");
        Apresentacao.Linha("  demo        Executa o fluxo completo ponta a ponta (modo simulação seguro).");
    }

    private static async Task ComandoColetar()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        Apresentacao.ImprimirJson(inventario);
    }

    private static async Task ComandoSanitizar()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var resultado = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario);

        Apresentacao.Titulo("Inventário seguro para nuvem");
        Apresentacao.ImprimirJson(resultado.InventarioSeguro);

        Apresentacao.Titulo("Relatório de sanitização (o que foi tratado antes de sair da máquina)");
        if (resultado.CamposAlterados.Count == 0)
        {
            Apresentacao.Linha("  (nenhum campo sensível encontrado)");
        }

        foreach (var campo in resultado.CamposAlterados)
        {
            Apresentacao.Item(campo.Campo, campo.Acao.ToString());
        }
    }

    private static void ComandoCatalogo()
    {
        var catalogo = CatalogoPadrao.Criar();
        Apresentacao.Linha($"Catálogo whitelisted (versão {catalogo.Versao}) — {catalogo.Todas.Count} ações");

        foreach (var acao in catalogo.Todas.OrderBy(a => a.Categoria).ThenBy(a => a.Id))
        {
            Apresentacao.Titulo($"{acao.Id}  [{acao.Categoria}]  risco={acao.Risco}");
            Apresentacao.Item("Título", acao.Titulo);
            Apresentacao.Item("Reinício", acao.RequerReinicio ? "sim" : "não");
            Apresentacao.Item("Pré-condições", string.Join(", ", acao.PreCondicoes));

            foreach (var parametro in acao.Parametros)
            {
                if (parametro is ParametroNumerico n)
                {
                    Apresentacao.Item(
                        $"param {n.Nome}",
                        $"seguro {n.FaixaSegura}, permitido {n.FaixaPermitida}, limite_absoluto {n.LimiteAbsoluto}, padrão {n.PadraoSeguro}{n.Unidade}");
                }
                else if (parametro is ParametroListaBranca l)
                {
                    Apresentacao.Item($"param {l.Nome}", "lista segura: " + string.Join(", ", l.ValoresSeguros));
                }
            }
        }
    }

    private static async Task ComandoRelatorio()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var relatorio = GerarRelatorioExecutivo(inventario, execucao: null);

        Apresentacao.Titulo("Relatório executivo (diagnóstico do equipamento)");
        ImprimirRelatorioExecutivo(relatorio);
    }

    private static RelatorioExecutivo GerarRelatorioExecutivo(Inventario inventario, RelatorioExecucao? execucao)
    {
        var validacoes = new List<ResultadoValidacao>();
        var alteracoes = new List<AlteracaoResumo>();
        var dominiosOtimizados = new HashSet<Dominio>();

        if (execucao is not null)
        {
            foreach (var categoria in execucao.Categorias)
            {
                if (categoria.Validacao is not null)
                {
                    validacoes.Add(categoria.Validacao);
                }

                if (categoria.Situacao == SituacaoCategoria.Aplicada)
                {
                    dominiosOtimizados.Add(MapearDominio(categoria.Categoria));
                }
            }

            foreach (var alteracao in execucao.TodasAlteracoes)
            {
                alteracoes.Add(new AlteracaoResumo(alteracao.Alvo, alteracao.ValorAnterior, alteracao.ValorNovo));
            }
        }

        return new GeradorRelatorio().Gerar(inventario, validacoes, alteracoes, dominiosOtimizados);
    }

    private static Dominio MapearDominio(CategoriaAcao categoria) => categoria switch
    {
        CategoriaAcao.Cpu => Dominio.Cpu,
        CategoriaAcao.Memoria => Dominio.Ram,
        CategoriaAcao.Gpu => Dominio.Gpu,
        _ => Dominio.Windows,
    };

    private static void ImprimirRelatorioExecutivo(RelatorioExecutivo relatorio)
    {
        Apresentacao.Item("Nota final", $"{relatorio.NotaFinal}/100 ({relatorio.Classificacao})");
        foreach (var score in relatorio.Scores.OrderBy(s => s.Dominio))
        {
            Apresentacao.Item(score.Dominio.ToString(), $"{score.Valor}/100 ({score.Classificacao})");
        }

        if (relatorio.Alteracoes.Count > 0)
        {
            Apresentacao.Linha("  Alterações:");
            foreach (var alteracao in relatorio.Alteracoes)
            {
                Apresentacao.Linha(
                    $"      {alteracao.Alvo}: {alteracao.Antes ?? "(não definido)"} -> {alteracao.Depois}");
            }
        }
    }

    private static async Task ComandoServir(string[] args)
    {
        var nome = args.Length > 1 ? args[1] : "hwopt-agente";
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Apresentacao.Linha($"Servidor IPC no pipe '{nome}'. Ctrl+C para encerrar.");
        var servidor = new ServidorNamedPipe(nome, new RoteadorIpc(logger: Log<RoteadorIpc>()), Log<ServidorNamedPipe>());

        try
        {
            await servidor.ServirAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // encerramento solicitado
        }
    }

    private static async Task ComandoIpcDemo()
    {
        var nome = "hwopt-demo-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var servidor = new ServidorNamedPipe(nome, new RoteadorIpc(logger: Log<RoteadorIpc>()), Log<ServidorNamedPipe>());
        var tarefa = servidor.ServirAsync(cts.Token);

        var cliente = new ClienteNamedPipe(nome);
        Apresentacao.Titulo("IPC demo (servidor + cliente em processo)");

        foreach (var metodo in new[] { "ping", "catalogo", "coletar", "sensores", "proposta", "relatorio" })
        {
            var resposta = await cliente.ChamarAsync(metodo, cts.Token);
            Apresentacao.Item(metodo, resposta.Sucesso ? "OK" : "ERRO: " + resposta.Erro);
        }

        // Fluxo de aprovação explícita por ação (a UI envia os IDs aprovados).
        var aprovacao = await cliente.ChamarAsync(
            new RequisicaoIpc
            {
                Metodo = "aprovar",
                Parametros = JsonSerializer.SerializeToElement(
                    new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO", "SO_EFEITOS_VISUAIS_DESEMPENHO" } }),
            },
            cts.Token);
        Apresentacao.Item("aprovar (2 ações)", aprovacao.Sucesso ? "executado e validado" : "ERRO: " + aprovacao.Erro);

        await cts.CancelAsync();
        try
        {
            await tarefa;
        }
        catch (OperationCanceledException)
        {
            // encerramento esperado
        }
    }

    private static async Task ComandoSensores()
    {
        var leitura = await new ServicoSensores(loggerFactory: _loggerFactory).LerAsync();

        Apresentacao.Titulo("Sensores (tempo real)");
        if (leitura.Sensores.Count == 0)
        {
            Apresentacao.Linha("  (nenhum sensor legível nesta máquina — driver/permissão ausente)");
            return;
        }

        foreach (var sensor in leitura.Sensores.OrderBy(s => s.Tipo).ThenBy(s => s.Nome, StringComparer.Ordinal))
        {
            Apresentacao.Item($"{sensor.Tipo} — {sensor.Nome}", $"{sensor.Valor} {sensor.Unidade}");
        }
    }

    private static async Task ComandoBios()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var repositorio = RepositorioSqlite.DeArquivo(
            Path.Combine(AppContext.BaseDirectory, "data", "otimizador.db"),
            Log<RepositorioSqlite>());
        await repositorio.InicializarAsync();

        // Banco curado com cache em SQLite (a busca web entraria como provedor interno futuro).
        var provedor = new ProvedorBiosComCache(
            new BancoCuradoBios(), repositorio, Log<ProvedorBiosComCache>());
        var relatorio = await new ModuloBios(provedor, Log<ModuloBios>()).AnalisarAsync(inventario);

        ImprimirRelatorioBios(relatorio);
    }

    private static void ImprimirRelatorioBios(RelatorioBios relatorio)
    {
        var id = relatorio.Identificacao;
        Apresentacao.Titulo("BIOS — Identificação");
        Apresentacao.Item("Fabricante", $"{id.Fabricante} (bruto: {id.FabricanteBruto})");
        Apresentacao.Item("Modelo", id.Modelo);
        Apresentacao.Item("Versão atual", id.VersaoAtual);
        Apresentacao.Item("Modo", id.Modo);
        Apresentacao.Item("Secure Boot", id.SecureBoot?.ToString());
        Apresentacao.Item("Fonte encontrada", relatorio.FonteEncontrada ? "sim (banco curado)" : "não");

        var decisao = relatorio.Decisao;
        Apresentacao.Titulo("BIOS — Decisão conservadora");
        Apresentacao.Item("Recomenda atualizar", decisao.RecomendaAtualizar ? "sim" : "não");
        Apresentacao.Item("Versão recomendada", decisao.VersaoRecomendada);
        Apresentacao.Item("Ganho", decisao.Ganho.ToString());
        Apresentacao.Item("Risco", decisao.Risco.ToString());
        Apresentacao.Item("Justificativa", decisao.Justificativa);
        Apresentacao.Item("Fonte", decisao.Fonte);

        var guia = relatorio.Guia;
        Apresentacao.Titulo("BIOS — Guia passo a passo");
        Apresentacao.Item("Tecla de setup", guia.TeclaSetup);
        Apresentacao.Item("Utilitário", guia.Utilitario);
        foreach (var passo in guia.Passos)
        {
            Apresentacao.Linha("   - " + passo);
        }

        Apresentacao.Linha("  Avisos:");
        foreach (var aviso in guia.Avisos)
        {
            Apresentacao.Linha("   ! " + aviso);
        }
    }

    private static async Task ComandoProposta()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var sanitizacao = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario);

        var matriz = await CriarCerebro().ProporAsync(sanitizacao.InventarioSeguro, CatalogoPadrao.Criar());
        ImprimirMatriz(matriz);
    }

    /// <summary>
    /// Usa o cérebro LLM quando HWOPT_LLM_MODELO e ANTHROPIC_API_KEY estão
    /// definidos no ambiente; caso contrário, usa o cérebro local (offline).
    /// O modelo nunca é fixado no código — vem da configuração.
    /// </summary>
    private static ICerebro CriarCerebro()
    {
        var modelo = Environment.GetEnvironmentVariable("HWOPT_LLM_MODELO");
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (!string.IsNullOrWhiteSpace(modelo) && !string.IsNullOrWhiteSpace(apiKey))
        {
            return new CerebroLlm(new ClienteLlmAnthropic(modelo, apiKey), Log<CerebroLlm>());
        }

        return new CerebroLocal();
    }

    private static async Task ComandoVisao(string[] args)
    {
        var modelo = Environment.GetEnvironmentVariable("HWOPT_LLM_MODELO");
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(apiKey))
        {
            Apresentacao.Linha(
                "O módulo de visão exige um modelo multimodal: defina ANTHROPIC_API_KEY e HWOPT_LLM_MODELO.");
            return;
        }

        if (args.Length < 2)
        {
            Apresentacao.Linha("Uso: hwopt visao <arquivo-de-imagem> [bios|etiqueta|erro|benchmark]");
            return;
        }

        var caminho = args[1];
        if (!File.Exists(caminho))
        {
            Apresentacao.Linha($"Arquivo não encontrado: {caminho}");
            return;
        }

        var caso = MapearCaso(args.Length > 2 ? args[2] : null);
        var imagem = ImagemEntrada.DeArquivo(caminho);

        var modulo = new ModuloVisao(new ClienteVisaoAnthropic(modelo, apiKey), Log<ModuloVisao>());
        var leitura = await modulo.InterpretarAsync(imagem, caso);

        Apresentacao.Titulo("Leitura visual");
        Apresentacao.Item("Tela", leitura.TipoTela.ToString());
        Apresentacao.Item("Confiança", leitura.Confianca.ToString());
        foreach (var campo in leitura.Campos)
        {
            Apresentacao.Item("  " + campo.Key, campo.Value);
        }

        Apresentacao.Item("Próximo passo", leitura.ProximoPasso);

        // Regra do documento: validar a leitura visual contra os dados coletados.
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var conferencia = new ConferenciaVisual().Conferir(leitura, inventario);

        Apresentacao.Titulo("Conferência com o inventário");
        Apresentacao.Item("Situação", conferencia.Situacao.ToString());
        Apresentacao.Item("Detalhe", conferencia.Mensagem);
        if (conferencia.PedirNovaFoto)
        {
            Apresentacao.Linha("   ! Recomenda-se enviar uma nova foto, mais nítida.");
        }
    }

    private static CasoUsoVisao MapearCaso(string? arg) => (arg ?? string.Empty).ToLowerInvariant() switch
    {
        "bios" => CasoUsoVisao.LerVersaoBios,
        "etiqueta" => CasoUsoVisao.LerEtiquetaPlaca,
        "erro" => CasoUsoVisao.LerMensagemErro,
        "benchmark" => CasoUsoVisao.LerBenchmark,
        _ => CasoUsoVisao.Identificar,
    };

    private static void ImprimirMatriz(MatrizDecisao matriz)
    {
        var origem = matriz.Modelo is null ? matriz.Origem.ToString() : $"{matriz.Origem}/{matriz.Modelo}";
        Apresentacao.Item("Origem", origem);
        foreach (var item in matriz.Itens)
        {
            var parametros = item.Parametros.Count == 0
                ? string.Empty
                : " [" + string.Join(", ", item.Parametros.Select(p => $"{p.Key}={p.Value}")) + "]";
            Apresentacao.Item(
                $"{item.Prioridade}. {item.AcaoId}",
                $"risco {item.Risco}, ganho {item.GanhoEsperado}{parametros}");
        }

        foreach (var aviso in matriz.Avisos)
        {
            Apresentacao.Linha("   ! " + aviso);
        }
    }

    private static async Task ComandoDemo()
    {
        var catalogo = CatalogoPadrao.Criar();
        var caminhoBanco = Path.Combine(AppContext.BaseDirectory, "data", "otimizador.db");
        var repositorio = RepositorioSqlite.DeArquivo(caminhoBanco, Log<RepositorioSqlite>());
        await repositorio.InicializarAsync();

        // Passo 1 — Coleta read-only.
        Apresentacao.Titulo("Passo 1 — Coleta de inventário (read-only)");
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        Apresentacao.Item("Placa", $"{inventario.Placa.Fabricante} {inventario.Placa.Modelo}");
        Apresentacao.Item("CPU", inventario.Cpu.Nome);
        Apresentacao.Item("SO", $"{inventario.SistemaOperacional.Nome} ({inventario.SistemaOperacional.Tipo})");
        await repositorio.SalvarInventarioAsync(inventario);

        var sensores = await new ServicoSensores(loggerFactory: _loggerFactory).LerAsync();
        Apresentacao.Item(
            "Sensores",
            $"{sensores.Sensores.Count} leitura(s)"
            + (sensores.TemperaturaMaxC is { } tmax ? $", temperatura máx {tmax} °C" : string.Empty));

        // Passo 2 — Sanitização (privacidade).
        Apresentacao.Titulo("Passo 2 — Sanitização (privacidade)");
        var sanitizacao = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario);
        Apresentacao.Item("Campos tratados", sanitizacao.CamposAlterados.Count.ToString());
        foreach (var campo in sanitizacao.CamposAlterados)
        {
            Apresentacao.Item(campo.Campo, campo.Acao.ToString());
        }

        // Passo 3 — Cérebro propõe (matriz de decisão; somente IDs do catálogo).
        Apresentacao.Titulo("Passo 3 — Cérebro propõe a matriz de decisão (inventário sanitizado)");
        var cerebro = CriarCerebro();
        var matriz = await cerebro.ProporAsync(sanitizacao.InventarioSeguro, catalogo);
        ImprimirMatriz(matriz);

        // Passo 4 — Perfil seguro a partir da matriz.
        var construtor = new ConstrutorPerfil(catalogo, Log<ConstrutorPerfil>());
        var perfilSeguro = construtor
            .CriarPerfilSeguro("perfil-seguro-demo", matriz.AcaoIds)
            .Perfil!;

        // Passo 5 — Backup obrigatório (bloqueante).
        Apresentacao.Titulo("Passo 4 — Backup obrigatório (bloqueante)");
        var backup = await new ServicoBackup(logger: Log<ServicoBackup>()).CriarBackupAsync(inventario);
        Apresentacao.Item("Backup confirmado", backup.Sucesso ? "sim" : "não");

        // Passo 6 — Execução controlada por categoria (modo simulação).
        Apresentacao.Titulo("Passo 5 — Execução controlada por categoria (modo simulação seguro)");
        var estado = new EstadoSistemaSimulado(new Dictionary<string, string>
        {
            ["registro:SystemResponsiveness"] = "20",
            ["powercfg:plano_ativo"] = "EQUILIBRADO",
        });
        var executor = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel(), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());

        var contexto = new ContextoExecucao { BackupConfirmado = backup.Sucesso };
        var relatorio = await executor.AplicarPerfilAsync(perfilSeguro, contexto);
        ImprimirRelatorio(relatorio);
        await repositorio.RegistrarExecucaoAsync(relatorio);

        // Passo 5b — Validação detecta regressão simulada e reverte automaticamente.
        Apresentacao.Titulo("Passo 5b — Validação detecta regressão e reverte automaticamente");
        var estadoRegressao = new EstadoSistemaSimulado(new Dictionary<string, string>
        {
            ["registro:SystemResponsiveness"] = "20",
        });
        var executorRegressao = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estadoRegressao),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.ComRegressao("whea"), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());
        var perfilRegressao = construtor.CriarPerfilSeguro("teste-regressao", new[] { "SO_SYSTEM_RESPONSIVENESS" }).Perfil!;
        var relRegressao = await executorRegressao.AplicarPerfilAsync(perfilRegressao, contexto);
        var categoriaRegressao = relRegressao.Categorias.Single();
        Apresentacao.Item("Categoria", categoriaRegressao.Categoria.ToString());
        Apresentacao.Item("Validação", categoriaRegressao.Validacao?.Estabilidade);
        Apresentacao.Item("Situação", categoriaRegressao.Situacao.ToString());
        Apresentacao.Item("Estado após rollback", estadoRegressao.Ler("registro:SystemResponsiveness") ?? "(restaurado)");

        // Passo 7 — Demonstração do perfil customizado e do consentimento.
        Apresentacao.Titulo("Passo 6 — Perfil customizado: bloqueio rígido por limite absoluto");
        var bloqueado = construtor.CriarPerfilCustomizado("custom-arriscado", "usuario", new[]
        {
            new SelecaoAcao
            {
                AcaoId = "SO_SYSTEM_RESPONSIVENESS",
                Parametros = new Dictionary<string, string> { ["percentual_reserva"] = "25" },
            },
        });
        Apresentacao.Item("Salvou?", bloqueado.Sucesso ? "sim" : "não (bloqueado)");
        foreach (var motivo in bloqueado.Bloqueios)
        {
            Apresentacao.Item("Bloqueio", motivo);
        }

        Apresentacao.Titulo("Passo 7 — Perfil customizado: risco assumido + consentimento");
        var customizado = construtor.CriarPerfilCustomizado("custom-demo", "usuario", new[]
        {
            new SelecaoAcao
            {
                AcaoId = "SO_SYSTEM_RESPONSIVENESS",
                Parametros = new Dictionary<string, string> { ["percentual_reserva"] = "5" },
            },
        });
        Apresentacao.Item("Válido?", customizado.Sucesso ? "sim" : "não");
        Apresentacao.Item("Exige consentimento?", customizado.ExigeConsentimento ? "sim" : "não");
        foreach (var risco in customizado.RiscosAssumidos)
        {
            Apresentacao.Item("Risco assumido", $"{risco.AcaoId}.{risco.Parametro} = {risco.Valor}");
        }

        await ProcessarConsentimento(customizado.Perfil!, estado, catalogo, repositorio);

        // Passo 8 — Auditoria.
        Apresentacao.Titulo("Passo 8 — Auditoria persistida (SQLite)");
        Apresentacao.Item("Inventários", (await repositorio.ContarInventariosAsync()).ToString());
        Apresentacao.Item("Consentimentos", (await repositorio.ContarConsentimentosAsync()).ToString());
        Apresentacao.Item("Execuções", (await repositorio.ContarExecucoesAsync()).ToString());
        Apresentacao.Linha();
        Apresentacao.Linha($"Banco: {caminhoBanco}");

        // Passo 9 — Relatório executivo e nota final.
        Apresentacao.Titulo("Passo 9 — Relatório executivo e nota final");
        var relatorioExecutivo = GerarRelatorioExecutivo(inventario, relatorio);
        ImprimirRelatorioExecutivo(relatorioExecutivo);
    }

    private static async Task ProcessarConsentimento(
        Perfil perfil, EstadoSistemaSimulado estado, CatalogoAcoes catalogo, IRepositorioOtimizacao repositorio)
    {
        var avaliador = new AvaliadorConsentimento(logger: Log<AvaliadorConsentimento>());
        var termo = avaliador.Termo;

        Apresentacao.Linha();
        Apresentacao.Linha("  >> " + termo.Titulo);
        foreach (var paragrafo in termo.CorpoAviso)
        {
            Apresentacao.Linha("     " + paragrafo);
        }

        // Simula o usuário marcando os dois checkboxes e confirmando.
        var resposta = new RespostaConsentimento(
            termo.Checkboxes.Select(c => c.Id), confirmacaoFinal: true);
        var consentimento = avaliador.Avaliar(resposta, perfil, catalogo.Versao);

        if (consentimento.Falha)
        {
            Apresentacao.Item("Consentimento", "recusado — " + consentimento.MensagemErro);
            return;
        }

        await repositorio.RegistrarConsentimentoAsync(consentimento.ValorObrigatorio);
        Apresentacao.Item("Consentimento", "registrado (auditoria gravada)");

        // Com consentimento registrado, o perfil pode ser aplicado.
        var perfilConsentido = perfil with { ConsentimentoRegistrado = true };
        var executor = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel(), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());

        var relatorio = await executor.AplicarPerfilAsync(
            perfilConsentido, new ContextoExecucao { BackupConfirmado = true });
        await repositorio.RegistrarExecucaoAsync(relatorio);
        Apresentacao.Item("Aplicado", relatorio.Sucesso ? "sim" : "não");
        Apresentacao.Item("SystemResponsiveness agora", estado.Ler("registro:SystemResponsiveness"));
    }

    private static void ImprimirRelatorio(RelatorioExecucao relatorio)
    {
        Apresentacao.Item("Perfil", relatorio.PerfilNome);
        Apresentacao.Item("Sucesso geral", relatorio.Sucesso ? "sim" : "não");
        foreach (var categoria in relatorio.Categorias)
        {
            Apresentacao.Item(categoria.Categoria.ToString(), categoria.Situacao.ToString());
            foreach (var alteracao in categoria.Alteracoes)
            {
                Apresentacao.Linha(
                    $"      {alteracao.Alvo}: {alteracao.ValorAnterior ?? "(não definido)"} -> {alteracao.ValorNovo}");
            }
        }
    }
}
