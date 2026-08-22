using System.Text.Json;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Licensing;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.Ipc.Tests;

public sealed class IpcTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6 },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    private static RoteadorIpc Roteador() => new(coletor: new ColetorFake(Inventario()));

    private static RequisicaoIpc Req(string metodo, object? parametros = null) => new()
    {
        Metodo = metodo,
        Parametros = parametros is null ? null : JsonSerializer.SerializeToElement(parametros),
    };

    // ---- RoteadorIpc (unitário) ----------------------------------------------

    [Fact]
    public async Task Ping_responde_pong()
    {
        var r = await Roteador().TratarAsync(Req("ping"));
        Assert.True(r.Sucesso);
        Assert.Equal("pong", r.Resultado);
    }

    [Fact]
    public async Task Metodo_desconhecido_falha()
    {
        var r = await Roteador().TratarAsync(Req("inexistente"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task Catalogo_retorna_lista_de_acoes()
    {
        var r = await Roteador().TratarAsync(Req("catalogo"));

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<AcaoResumoDto>>(r.Resultado);
        Assert.NotEmpty(lista);
        Assert.Contains(lista, a => a.Id == "PWR_PLANO_ALTO_DESEMPENHO");
    }

    [Fact]
    public async Task Coletar_retorna_o_inventario()
    {
        var r = await Roteador().TratarAsync(Req("coletar"));

        Assert.True(r.Sucesso);
        var inv = Assert.IsType<Inventario>(r.Resultado);
        Assert.Equal("ASUS", inv.Placa.Fabricante);
    }

    [Fact]
    public async Task Proposta_retorna_matriz_de_decisao()
    {
        var r = await Roteador().TratarAsync(Req("proposta"));

        Assert.True(r.Sucesso);
        var matriz = Assert.IsType<Cerebro.MatrizDecisao>(r.Resultado);
        Assert.NotEmpty(matriz.Itens);
    }

    [Fact]
    public async Task Aprovar_acoes_executa_e_retorna_relatorio()
    {
        var r = await Roteador().TratarAsync(Req("aprovar", new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO" } }));

        Assert.True(r.Sucesso);
        var relatorio = Assert.IsType<Agent.Execution.RelatorioExecucao>(r.Resultado);
        Assert.True(relatorio.Sucesso);
    }

    [Fact]
    public async Task Aprovar_sem_acoes_falha()
    {
        var r = await Roteador().TratarAsync(Req("aprovar", new { acoes = Array.Empty<string>() }));
        Assert.False(r.Sucesso);
    }

    // ---- Loopback real de named pipe -----------------------------------------

    [Fact]
    public async Task NamedPipe_loopback_responde_requisicoes()
    {
        var nome = "hwopt-test-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var servidor = new ServidorNamedPipe(nome, Roteador());
        var tarefaServidor = servidor.ServirAsync(cts.Token);

        var cliente = new ClienteNamedPipe(nome);
        var ping = await cliente.ChamarAsync("ping", cts.Token);
        var catalogo = await cliente.ChamarAsync("catalogo", cts.Token);

        Assert.True(ping.Sucesso);
        Assert.True(catalogo.Sucesso);

        await cts.CancelAsync();
        try
        {
            await tarefaServidor;
        }
        catch (OperationCanceledException)
        {
            // encerramento esperado
        }
    }

    // ---- exportarbackupdrivers --------------------------------------------------

    [Fact]
    public async Task ExportarBackupDrivers_NaoWindows_RetornaFalha()
    {
        if (OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("exportarbackupdrivers"));
        Assert.False(r.Sucesso);
        Assert.Contains("Windows", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportarBackupDrivers_Windows_CriaSubpasta()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("exportarbackupdrivers"));

        var raiz = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OtimizeBuilder", "DriverBackups");

        // A pasta base sempre deve ser criada, independentemente do pnputil ter êxito.
        Assert.True(Directory.Exists(raiz), $"Esperava: {raiz}");

        // Se pnputil teve êxito, Resultado deve ser a pasta com timestamp.
        if (r.Sucesso)
            Assert.IsType<string>(r.Resultado);
    }

    // ---- varrerdrivers ------------------------------------------------------------

    [Fact]
    public async Task VarrerDrivers_NaoWindows_RetornaFalha()
    {
        if (OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("varrerdrivers"));
        Assert.False(r.Sucesso);
        Assert.Contains("Windows", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VarrerDrivers_Windows_RetornaListaDeDrivers()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("varrerdrivers"));
        Assert.True(r.Sucesso);
        Assert.IsAssignableFrom<IReadOnlyList<InfoDriver>>(r.Resultado);
    }

    // ---- aprovaratualizacaodriver -------------------------------------------------

    [Fact]
    public async Task AprovarAtualizacaoDriver_SemParametros_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("aprovaratualizacaodriver"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task AprovarAtualizacaoDriver_UrlVazia_RetornaFalha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("aprovaratualizacaodriver", new { urlDownload = "" }));
        Assert.False(r.Sucesso);
        Assert.Contains("urlDownload", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ---- reverteratualizacaodriver --------------------------------------------------

    [Fact]
    public async Task ReverterAtualizacaoDriver_SemParametros_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("reverteratualizacaodriver"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task ReverterAtualizacaoDriver_BackupInexistente_RetornaFalhaClara()
    {
        if (!OperatingSystem.IsWindows()) return;

        var caminho = Path.Combine(Path.GetTempPath(), "hwopt-backup-inexistente-" + Guid.NewGuid().ToString("N"));
        var r = await Roteador().TratarAsync(Req("reverteratualizacaodriver", new { caminhoBackup = caminho }));
        Assert.False(r.Sucesso);
        Assert.Contains("não encontrado", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ---- verificarsoftware -----------------------------------------------------
    // Cobre "conecta o fluxo real" da spec-1-3-software-desatualizado: o roteador
    // de fato monta VerificadorSoftware + ProvedorFonteOficialSoftware +
    // RepositorioVersoesSoftwareEstatico (catálogo embarcado real), não um fake.

    [Fact]
    public async Task VerificarSoftware_SemParametros_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("verificarsoftware"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task VerificarSoftware_ListaVazia_RetornaListaVazia()
    {
        var r = await Roteador().TratarAsync(Req("verificarsoftware", new { programas = Array.Empty<object>() }));

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<InfoSoftware>>(r.Resultado);
        Assert.Empty(lista);
    }

    [Fact]
    public async Task VerificarSoftware_ProgramaDoCatalogoComVersaoDiferente_RetornaItemComLink()
    {
        var r = await Roteador().TratarAsync(Req("verificarsoftware", new
        {
            programas = new[] { new { nome = "7-Zip 21.07 (x64)", versao = "21.07" } },
        }));

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<InfoSoftware>>(r.Resultado);
        var item = Assert.Single(lista);
        Assert.Equal("7-Zip 21.07 (x64)", item.Nome);
        Assert.Equal("21.07", item.VersaoAtual);
        Assert.False(string.IsNullOrEmpty(item.VersaoDisponivel));
        Assert.False(string.IsNullOrEmpty(item.UrlDownload));
        Assert.Equal(StatusSoftware.AtualizacaoDisponivel, item.Status);
    }

    [Fact]
    public async Task VerificarSoftware_ProgramaSemCoberturaNoCatalogo_NaoAparece()
    {
        var r = await Roteador().TratarAsync(Req("verificarsoftware", new
        {
            programas = new[] { new { nome = "Programa Totalmente Desconhecido XYZ", versao = "1.0" } },
        }));

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<InfoSoftware>>(r.Resultado);
        Assert.Empty(lista);
    }

    /// <summary>
    /// Regressão do bug de casing self-caught na Story 1.3: serializa um
    /// <see cref="ProgramaInstalado"/> real (propriedades PascalCase em C#) usando
    /// exatamente <see cref="ProtocoloIpc.Json"/> — o mesmo codec e o mesmo shape
    /// de payload (<c>new { programas = ... }</c>) que <c>DriversViewModel.VerificarSoftwareAsync</c>
    /// usa em produção — em vez do <c>Req()</c> helper (que usa opções default e
    /// fixtures já escritas em camelCase, não pega esse tipo de regressão).
    /// </summary>
    [Fact]
    public async Task VerificarSoftware_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto()
    {
        var programas = new List<ProgramaInstalado>
        {
            new() { Nome = "7-Zip 21.07 (x64)", Versao = "21.07" },
        };
        var parametros = JsonSerializer.SerializeToElement(new { programas }, ProtocoloIpc.Json);
        var req = new RequisicaoIpc { Metodo = "verificarsoftware", Parametros = parametros };

        var r = await Roteador().TratarAsync(req);

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<InfoSoftware>>(r.Resultado);
        var item = Assert.Single(lista);
        Assert.Equal("7-Zip 21.07 (x64)", item.Nome);
        Assert.Equal("21.07", item.VersaoAtual);
    }

    // ---- verificarbios -----------------------------------------------------------
    // Cobre "conecta o fluxo real" da spec-1-4-bios-alerta-risco: o roteador de
    // fato monta VerificadorBios + ProvedorFonteOficialBios + BancoCuradoBios
    // (catálogo embarcado real), não um fake.

    [Fact]
    public async Task VerificarBios_SemParametros_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("verificarbios"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task VerificarBios_PlacaDoCatalogoComVersaoAntiga_RetornaAlertaComGuia()
    {
        var r = await Roteador().TratarAsync(Req("verificarbios", new
        {
            placa = new { fabricante = "ASUS", modelo = "ROG Strix B550-F", versaoBios = "2806" },
        }));

        Assert.True(r.Sucesso);
        var info = Assert.IsType<InfoBios>(r.Resultado);
        Assert.Equal("ASUS", info.Fabricante);
        Assert.Equal("2806", info.VersaoAtual);
        Assert.False(string.IsNullOrEmpty(info.VersaoDisponivel));
        Assert.False(string.IsNullOrEmpty(info.TeclaSetup));
        Assert.False(string.IsNullOrEmpty(info.Utilitario));
        Assert.NotEmpty(info.Passos);
        Assert.NotEmpty(info.Avisos);
    }

    [Fact]
    public async Task VerificarBios_PlacaComVersaoJaAtualizada_RetornaNullSemAlerta()
    {
        var r = await Roteador().TratarAsync(Req("verificarbios", new
        {
            placa = new { fabricante = "ASUS", modelo = "ROG Strix B550-F", versaoBios = "3405" },
        }));

        Assert.True(r.Sucesso);
        Assert.Null(r.Resultado);
    }

    [Fact]
    public async Task VerificarBios_PlacaSemCoberturaNoCatalogo_RetornaNullSemAlerta()
    {
        var r = await Roteador().TratarAsync(Req("verificarbios", new
        {
            placa = new { fabricante = "Placa Desconhecida XYZ", modelo = "Modelo Qualquer", versaoBios = "1.0" },
        }));

        Assert.True(r.Sucesso);
        Assert.Null(r.Resultado);
    }

    [Fact]
    public async Task VerificarBios_ParametroPlacaAusente_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("verificarbios", new { outraCoisa = "x" }));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    /// <summary>
    /// Mesma regressão de casing coberta para "verificarsoftware": serializa uma
    /// <see cref="PlacaMae"/> real (propriedades PascalCase em C#) usando
    /// exatamente <see cref="ProtocoloIpc.Json"/> — o mesmo codec e o mesmo shape
    /// de payload (<c>new { placa = ... }</c>) que
    /// <c>DriversViewModel.VerificarBiosAsync</c> usa em produção.
    /// </summary>
    [Fact]
    public async Task VerificarBios_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto()
    {
        var placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG Strix B550-F", VersaoBios = "2806" };
        var parametros = JsonSerializer.SerializeToElement(new { placa }, ProtocoloIpc.Json);
        var req = new RequisicaoIpc { Metodo = "verificarbios", Parametros = parametros };

        var r = await Roteador().TratarAsync(req);

        Assert.True(r.Sucesso);
        var info = Assert.IsType<InfoBios>(r.Resultado);
        Assert.Equal("ASUS", info.Fabricante);
        Assert.Equal("2806", info.VersaoAtual);
    }

    // ---- analisarbiosfoto -------------------------------------------------------

    [Fact]
    public async Task AnalisarBiosFoto_SemParametros_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("analisarbiosfoto"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task AnalisarBiosFoto_SemBase64_RetornaFalha()
    {
        var r = await Roteador().TratarAsync(Req("analisarbiosfoto", new { mediaType = "image/png" }));
        Assert.False(r.Sucesso);
        Assert.Contains("imagemBase64", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ---- obterstatuslicenca ---------------------------------------------------

    [Fact]
    public async Task ObterStatusLicenca_SemLicenca_RetornaGratuita()
    {
        var r = await Roteador().TratarAsync(Req("obterstatuslicenca"));
        Assert.True(r.Sucesso);
        var dto = Assert.IsType<StatusLicencaDto>(r.Resultado);
        Assert.Equal("Gratuita", dto.Tipo);
        Assert.False(dto.ModuloUpgrade);
        Assert.False(dto.ContadorVidaUtil);
        Assert.False(dto.GerenciadorDrivers);
        Assert.False(dto.GuiaBiosIa);
    }

    [Fact]
    public async Task ObterStatusLicenca_ComLicencaGratuita_SemAcesso()
    {
        var roteador = new RoteadorIpc(
            coletor: new ColetorFake(Inventario()),
            licenca: new LicencaFake(TipoLicenca.Gratuita));
        var r = await roteador.TratarAsync(Req("obterstatuslicenca"));
        Assert.True(r.Sucesso);
        var dto = Assert.IsType<StatusLicencaDto>(r.Resultado);
        Assert.Equal("Gratuita", dto.Tipo);
        Assert.False(dto.ModuloUpgrade);
        Assert.False(dto.GuiaBiosIa);
    }

    [Fact]
    public async Task ObterStatusLicenca_ComLicencaPremium_TodasFuncionalidades()
    {
        var roteador = new RoteadorIpc(
            coletor: new ColetorFake(Inventario()),
            licenca: new LicencaFake(TipoLicenca.Premium));
        var r = await roteador.TratarAsync(Req("obterstatuslicenca"));
        Assert.True(r.Sucesso);
        var dto = Assert.IsType<StatusLicencaDto>(r.Resultado);
        Assert.Equal("Premium", dto.Tipo);
        Assert.True(dto.ModuloUpgrade);
        Assert.True(dto.ContadorVidaUtil);
        Assert.True(dto.GerenciadorDrivers);
        Assert.True(dto.GuiaBiosIa);
    }

    // ── obterfeatures / habilitarfeature / desabilitarfeature ──────────────

    [Fact]
    public async Task ObterFeatures_NaoWindows_RetornaFalha()
    {
        if (OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("obterfeatures"));
        Assert.False(r.Sucesso);
        Assert.Contains("Windows", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HabilitarFeature_SemNome_RetornaFalha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("habilitarfeature"));
        Assert.False(r.Sucesso);
        Assert.Contains("nome", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HabilitarFeature_NomeForaDaLista_RetornaFalha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("habilitarfeature", new { nome = "FeatureNaoExistente123" }));
        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task DesabilitarFeature_SemNome_RetornaFalha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("desabilitarfeature"));
        Assert.False(r.Sucesso);
        Assert.Contains("nome", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesabilitarFeature_NomeForaDaLista_RetornaFalha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("desabilitarfeature", new { nome = "FeatureNaoExistente123" }));
        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task Catalogo_inclui_features_windows()
    {
        var r = await Roteador().TratarAsync(Req("catalogo"));
        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<AcaoResumoDto>>(r.Resultado);
        Assert.Contains(lista, a => a.Id == "FEATURE_WSL");
        Assert.Contains(lista, a => a.Id == "FEATURE_HYPER_V");
    }

    // ---- diagnosticarcausaraiz -----------------------------------------------------
    // Cobre "conecta o fluxo real" da spec-1-5-causa-raiz-event-log: o roteador
    // de fato monta LeitorEventLog + CorrelacionadorCausaRaiz (leitura real do
    // Event Log quando em Windows), não um fake.

    [Fact]
    public async Task DiagnosticarCausaRaiz_NaoWindows_RetornaFalha()
    {
        if (OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("diagnosticarcausaraiz"));
        Assert.False(r.Sucesso);
        Assert.Contains("Windows", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiagnosticarCausaRaiz_Windows_SemParametros_RetornaListaDeEventos()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("diagnosticarcausaraiz"));
        Assert.True(r.Sucesso);
        Assert.IsAssignableFrom<IReadOnlyList<EventoInstabilidade>>(r.Resultado);
    }

    [Fact]
    public async Task DiagnosticarCausaRaiz_Windows_ComDriversEBios_NaoLanca()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("diagnosticarcausaraiz", new
        {
            driversDesatualizados = new[]
            {
                new { hardwareId = "PCI\\VEN_10DE", descricao = "GeForce RTX 3060", fabricante = "NVIDIA" },
            },
            bios = new { fabricante = "ASUS", modelo = "ROG Strix B550-F", versaoAtual = "2806", teclaSetup = "Del", utilitario = "EZ Flash 3" },
        }));

        Assert.True(r.Sucesso);
        Assert.IsAssignableFrom<IReadOnlyList<EventoInstabilidade>>(r.Resultado);
    }

    [Fact]
    public async Task DiagnosticarCausaRaiz_Windows_ParametroDriversInvalido_RetornaFalha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("diagnosticarcausaraiz", new
        {
            driversDesatualizados = "não é um array",
        }));

        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task DiagnosticarCausaRaiz_Windows_ArrayComElementoNulo_NaoLancaEIgnoraONulo()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Achado da revisão independente: [null] no array desserializava pra um
        // InfoDriver? nulo dentro da lista, e o acesso a d.Fabricante em
        // CorrelacionadorCausaRaiz lançaria NullReferenceException não capturada.
        var r = await Roteador().TratarAsync(Req("diagnosticarcausaraiz", new
        {
            driversDesatualizados = new object?[] { null },
        }));

        Assert.True(r.Sucesso);
        Assert.IsAssignableFrom<IReadOnlyList<EventoInstabilidade>>(r.Resultado);
    }

    [Fact]
    public async Task DiagnosticarCausaRaiz_Windows_DriversDesatualizadosNuloExplicito_TratadoComoListaVazia()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Mesmo tratamento já dado ao parâmetro 'bios': null explícito é
        // equivalente a omitir o parâmetro, não um erro do chamador.
        var r = await Roteador().TratarAsync(Req("diagnosticarcausaraiz", new
        {
            driversDesatualizados = (object?)null,
        }));

        Assert.True(r.Sucesso);
        Assert.IsAssignableFrom<IReadOnlyList<EventoInstabilidade>>(r.Resultado);
    }

    /// <summary>
    /// Regressão da mesma classe de bug de casing self-caught nas Stories 1.3/1.4:
    /// serializa <see cref="InfoDriver"/>/<see cref="InfoBios"/> reais (propriedades
    /// PascalCase em C#) usando exatamente <see cref="ProtocoloIpc.Json"/> — o
    /// mesmo codec e o mesmo shape de payload que <c>DriversViewModel.DiagnosticarCausaRaizAsync</c>
    /// usa em produção — em vez do helper <c>Req()</c> (que usa opções default e
    /// fixtures já escritas à mão em camelCase, não pega esse tipo de regressão).
    /// </summary>
    [Fact]
    public async Task DiagnosticarCausaRaiz_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto()
    {
        if (!OperatingSystem.IsWindows()) return;

        var driversDesatualizados = new List<InfoDriver>
        {
            new()
            {
                HardwareId = "PCI\\VEN_10DE&DEV_2504",
                Descricao = "GeForce RTX 3060",
                Fabricante = "NVIDIA",
                Status = StatusDriver.AtualizacaoDisponivel,
            },
        };
        var bios = new InfoBios
        {
            Fabricante = "ASUS",
            Modelo = "ROG STRIX B550-F",
            VersaoAtual = "2806",
            VersaoDisponivel = "3405",
            TeclaSetup = "Del",
            Utilitario = "EZ Flash 3",
        };
        var parametros = JsonSerializer.SerializeToElement(
            new { driversDesatualizados, bios }, ProtocoloIpc.Json);
        var req = new RequisicaoIpc { Metodo = "diagnosticarcausaraiz", Parametros = parametros };

        var r = await Roteador().TratarAsync(req);

        Assert.True(r.Sucesso);
        Assert.IsAssignableFrom<IReadOnlyList<EventoInstabilidade>>(r.Resultado);
    }

    private sealed class ColetorFake : IColetorInventario
    {
        private readonly Inventario _inventario;

        public ColetorFake(Inventario inventario) => _inventario = inventario;

        public Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }

    private sealed class LicencaFake : IServicoLicenca
    {
        private readonly TipoLicenca _tipo;

        public LicencaFake(TipoLicenca tipo) => _tipo = tipo;

        public TipoLicenca TipoAtual => _tipo;
        public string? NomeCliente => null;
        public string? EmailCliente => null;

        public bool TemAcesso(FuncionalidadePremium _) => _tipo == TipoLicenca.Premium;

        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(ResultadoAtivacao.Ok(_tipo));

        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default) =>
            Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));

        public Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default) =>
            Task.FromResult(ResultadoAtivacao.Ok(_tipo));
    }
}
