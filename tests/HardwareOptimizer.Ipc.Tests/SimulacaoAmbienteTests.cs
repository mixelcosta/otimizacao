using System.Text.Json;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Licensing;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.Ipc.Tests;

// ============================================================================
// Fixture: servidor de named pipe real com dependências 100 % simuladas.
// Inicializa uma única instância de RoteadorIpc com ColetorFake e serve todas
// as requisições dos testes da classe SimulacaoAmbienteTests via OS pipe.
// ============================================================================

public sealed class AmbienteSimuladoFixture : IAsyncLifetime
{
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromMinutes(2));
    private Task _servidor = Task.CompletedTask;

    public string NomePipe { get; } = "hwopt-sim-" + Guid.NewGuid().ToString("N");

    public Task InitializeAsync()
    {
        var inv = new Inventario
        {
            Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "3001" },
            Cpu   = new Processador { Nome = "AMD Ryzen 5 5600X", Nucleos = 6 },
            Gpu   = new[] { new PlacaVideo { Nome = "NVIDIA GeForce RTX 3060" } },
            SistemaOperacional = new SistemaOperacionalInfo
            {
                Tipo = SistemaOperacionalTipo.Windows,
                Arquitetura = "X64",
            },
        };

        var roteador = new RoteadorIpc(coletor: new ColetorFake(inv));
        var servidor  = new ServidorNamedPipe(NomePipe, roteador);

        // ServirAsync cria o NamedPipeServerStream ANTES do primeiro await,
        // portanto o pipe já está escutando quando o Task é devolvido.
        _servidor = servidor.ServirAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _cts.CancelAsync();
        try { await _servidor; }
        catch (OperationCanceledException) { }
        _cts.Dispose();
    }

    public Task<RespostaIpc> ChamarAsync(string metodo) =>
        new ClienteNamedPipe(NomePipe).ChamarAsync(
            new RequisicaoIpc { Metodo = metodo }, _cts.Token);

    public Task<RespostaIpc> ChamarAsync(string metodo, object parametros) =>
        new ClienteNamedPipe(NomePipe).ChamarAsync(
            new RequisicaoIpc
            {
                Metodo     = metodo,
                Parametros = JsonSerializer.SerializeToElement(parametros),
            },
            _cts.Token);

    /// <summary>
    /// Desserializa <see cref="RespostaIpc.Resultado"/>: quando o resultado chega
    /// via pipe é um <see cref="JsonElement"/>; quando via roteador direto é o
    /// tipo CLR original.
    /// </summary>
    public T? DeserializarResultado<T>(RespostaIpc resp)
    {
        if (resp.Resultado is JsonElement el)
            return JsonSerializer.Deserialize<T>(el.GetRawText(), ProtocoloIpc.Json);
        return (T?)resp.Resultado;
    }

    private sealed class ColetorFake : IColetorInventario
    {
        private readonly Inventario _inv;
        public ColetorFake(Inventario inv) => _inv = inv;
        public Task<Inventario> ColetarAsync(CancellationToken ct = default) => Task.FromResult(_inv);
    }
}

// ============================================================================
// Testes via transporte real de named pipe
// O fixture sobe um servidor OS-level; cada teste abre uma conexão separada.
// ============================================================================

public sealed class SimulacaoAmbienteTests : IClassFixture<AmbienteSimuladoFixture>
{
    private readonly AmbienteSimuladoFixture _env;
    public SimulacaoAmbienteTests(AmbienteSimuladoFixture env) => _env = env;

    // ── Protocolo ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Protocolo_ping_retorna_pong_via_pipe()
    {
        var r = await _env.ChamarAsync("ping");

        Assert.True(r.Sucesso);
        var val = Assert.IsType<JsonElement>(r.Resultado);
        Assert.Equal("pong", val.GetString());
    }

    [Fact]
    public async Task Protocolo_metodo_desconhecido_retorna_falha_via_pipe()
    {
        var r = await _env.ChamarAsync("metodo_inexistente_xyz_simulacao");

        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    // ── Catálogo ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Catalogo_via_pipe_todos_itens_tem_id_e_titulo_preenchidos()
    {
        var r = await _env.ChamarAsync("catalogo");
        Assert.True(r.Sucesso);

        var lista = _env.DeserializarResultado<List<AcaoResumoDto>>(r)!;
        Assert.NotEmpty(lista);

        Assert.All(lista, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Id),    $"Item sem Id.");
            Assert.False(string.IsNullOrWhiteSpace(a.Titulo),$"Item '{a.Id}' sem Titulo.");
        });
    }

    [Fact]
    public async Task Catalogo_via_pipe_abrange_pelo_menos_5_categorias_distintas()
    {
        var r = await _env.ChamarAsync("catalogo");
        Assert.True(r.Sucesso);

        var lista     = _env.DeserializarResultado<List<AcaoResumoDto>>(r)!;
        var categorias = lista.Select(a => a.Categoria).Distinct().Count();

        Assert.True(categorias >= 5,
            $"Esperava >= 5 categorias no catálogo, mas encontrou {categorias}.");
    }

    // ── Inventário ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Inventario_via_pipe_preserva_dados_do_coletor_simulado()
    {
        var r = await _env.ChamarAsync("coletar");
        Assert.True(r.Sucesso);

        var resultado  = Assert.IsType<JsonElement>(r.Resultado);
        var fabricante = resultado.GetProperty("placa").GetProperty("fabricante").GetString();
        var cpu        = resultado.GetProperty("cpu").GetProperty("nome").GetString();

        Assert.Equal("ASUS",             fabricante);
        Assert.Equal("AMD Ryzen 5 5600X", cpu);
    }

    // ── Proposta ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Proposta_via_pipe_itens_tem_acaoId_e_justificativa_preenchidos()
    {
        var r = await _env.ChamarAsync("proposta");
        Assert.True(r.Sucesso);

        var resultado = Assert.IsType<JsonElement>(r.Resultado);
        Assert.True(resultado.TryGetProperty("itens", out var itens),
            "Resposta de 'proposta' deve conter a propriedade 'itens'.");
        Assert.True(itens.GetArrayLength() > 0, "A lista de itens não deve ser vazia.");

        foreach (var item in itens.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("acaoId", out var acaoId),
                "Cada item de proposta deve ter 'acaoId'.");
            Assert.False(string.IsNullOrWhiteSpace(acaoId.GetString()),
                "O 'acaoId' do item não deve ser vazio.");

            Assert.True(item.TryGetProperty("justificativa", out var just),
                "Cada item de proposta deve ter 'justificativa'.");
            Assert.False(string.IsNullOrWhiteSpace(just.GetString()),
                "A 'justificativa' do item não deve ser vazia.");
        }
    }

    // ── Aprovação / Execução ──────────────────────────────────────────────────

    [Fact]
    public async Task Aprovacao_acao_simples_via_pipe_retorna_relatorio_com_sucesso()
    {
        var r = await _env.ChamarAsync("aprovar",
            new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO" } });

        Assert.True(r.Sucesso, r.Erro ?? "(sem mensagem de erro)");
        var relatorio = _env.DeserializarResultado<RelatorioExecucao>(r)!;
        Assert.True(relatorio.Sucesso);
        Assert.NotEmpty(relatorio.Categorias);
    }

    [Fact]
    public async Task Aprovacao_2_categorias_distintas_via_pipe_gera_relatorio_multimodulo()
    {
        // Obtém 2 IDs de categorias distintas dinamicamente para que o teste
        // continue funcionando mesmo se o catálogo for estendido ou reorganizado.
        var catResp  = await _env.ChamarAsync("catalogo");
        var catalogo = _env.DeserializarResultado<List<AcaoResumoDto>>(catResp)!;

        var ids = catalogo
            .GroupBy(a => a.Categoria)
            .Take(2)
            .Select(g => g.First().Id)
            .ToArray();

        Assert.Equal(2, ids.Length);

        var r = await _env.ChamarAsync("aprovar", new { acoes = ids });
        Assert.True(r.Sucesso, r.Erro ?? "(sem mensagem de erro)");

        var relatorio = _env.DeserializarResultado<RelatorioExecucao>(r)!;
        Assert.True(relatorio.Sucesso);
        Assert.Equal(2, relatorio.Categorias.Count);
    }

    [Fact]
    public async Task Aprovacao_com_nome_perfil_personalizado_aparece_no_relatorio()
    {
        var r = await _env.ChamarAsync("aprovar", new
        {
            acoes      = new[] { "PWR_PLANO_ALTO_DESEMPENHO" },
            nomePerfil = "perfil-simulacao-teste",
        });

        Assert.True(r.Sucesso, r.Erro ?? "(sem mensagem de erro)");
        var relatorio = _env.DeserializarResultado<RelatorioExecucao>(r)!;
        Assert.Equal("perfil-simulacao-teste", relatorio.PerfilNome);
    }

    // ── Licença ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusLicenca_via_pipe_retorna_dto_gratuita_com_todos_campos()
    {
        var r = await _env.ChamarAsync("obterstatuslicenca");
        Assert.True(r.Sucesso);

        var dto = _env.DeserializarResultado<StatusLicencaDto>(r)!;
        Assert.Equal("Gratuita", dto.Tipo);
        Assert.False(dto.ModuloUpgrade);
        Assert.False(dto.ContadorVidaUtil);
        Assert.False(dto.GerenciadorDrivers);
        Assert.False(dto.GuiaBiosIa);
    }

    // ── Windows: Recursos Opcionais ───────────────────────────────────────────

    [Fact]
    public async Task Features_Windows_via_pipe_retorna_5_itens_com_campos_obrigatorios()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await _env.ChamarAsync("obterfeatures");
        Assert.True(r.Sucesso);

        var features = _env.DeserializarResultado<List<InfoFeatureWindows>>(r)!;
        Assert.Equal(5, features.Count);

        Assert.All(features, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Nome),
                $"Feature sem Nome.");
            Assert.False(string.IsNullOrWhiteSpace(f.NomeExibicao),
                $"Feature '{f.Nome}' sem NomeExibicao.");
            Assert.False(string.IsNullOrWhiteSpace(f.Descricao),
                $"Feature '{f.Nome}' sem Descricao.");
        });
    }

    // ── Windows: Efeitos Visuais ──────────────────────────────────────────────

    [Fact]
    public async Task EfeitosVisuais_Windows_via_pipe_retorna_lista_nao_vazia_com_id_e_nome()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await _env.ChamarAsync("obterefeitosvisuais");
        Assert.True(r.Sucesso);

        var efeitos = _env.DeserializarResultado<List<EfeitoVisual>>(r)!;
        Assert.NotEmpty(efeitos);

        Assert.All(efeitos, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Id),   "Efeito sem Id.");
            Assert.False(string.IsNullOrWhiteSpace(e.Nome), $"Efeito '{e.Id}' sem Nome.");
        });
    }

    // ── Windows: Limpeza ─────────────────────────────────────────────────────

    [Fact]
    public async Task Limpeza_Windows_via_pipe_scan_retorna_8_categorias_com_id_e_nome()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await _env.ChamarAsync("escanearlimpeza");
        Assert.True(r.Sucesso);

        var cats = _env.DeserializarResultado<List<CategoriaLimpeza>>(r)!;
        Assert.Equal(8, cats.Count);

        Assert.All(cats, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Id),   "Categoria de limpeza sem Id.");
            Assert.False(string.IsNullOrWhiteSpace(c.Nome), $"Categoria '{c.Id}' sem Nome.");
        });
    }

    // ── Resiliência do servidor ────────────────────────────────────────────────

    [Fact]
    public async Task Pipe_responde_5_requisicoes_sequenciais_sem_erros()
    {
        for (int i = 0; i < 5; i++)
        {
            var r = await _env.ChamarAsync("ping");
            Assert.True(r.Sucesso, $"Falha na requisição #{i + 1}.");
        }
    }
}

// ============================================================================
// Cenários avançados via roteador direto (sem transporte de pipe).
// Cobre validações de parâmetros, segurança e lógica de negócio.
// ============================================================================

public sealed class CenariosAvancadosTests
{
    private static RoteadorIpc Roteador(IServicoLicenca? licenca = null) =>
        new(coletor: new ColetorFake(), licenca: licenca);

    private static RequisicaoIpc Req(string metodo, object? parametros = null) => new()
    {
        Metodo     = metodo,
        Parametros = parametros is null ? null : JsonSerializer.SerializeToElement(parametros),
    };

    // ── Aprovação ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aprovacao_sem_nome_perfil_usa_padrao_perfil_ipc()
    {
        var r = await Roteador().TratarAsync(
            Req("aprovar", new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO" } }));

        Assert.True(r.Sucesso);
        var relatorio = Assert.IsType<RelatorioExecucao>(r.Resultado);
        Assert.Equal("perfil-ipc", relatorio.PerfilNome);
    }

    [Fact]
    public async Task Aprovacao_id_inexistente_retorna_mensagem_de_perfil_invalido()
    {
        var r = await Roteador().TratarAsync(
            Req("aprovar", new { acoes = new[] { "ACAO_QUE_NAO_EXISTE_XYZ_123" } }));

        Assert.False(r.Sucesso);
        Assert.Contains("Perfil inválido", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ── Catálogo ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Catalogo_todas_acoes_features_windows_tem_categoria_correta()
    {
        var r = await Roteador().TratarAsync(Req("catalogo"));
        Assert.True(r.Sucesso);

        var lista     = Assert.IsAssignableFrom<IReadOnlyList<AcaoResumoDto>>(r.Resultado);
        var features  = lista.Where(a => a.Id.StartsWith("FEATURE_", StringComparison.Ordinal)).ToList();

        Assert.True(features.Count >= 4,
            $"Esperava >= 4 ações FEATURE_* no catálogo, mas encontrou {features.Count}.");
        Assert.All(features, f =>
            Assert.Equal(CategoriaAcao.FeaturesWindows, f.Categoria));
    }

    // ── Licença Premium ───────────────────────────────────────────────────────

    [Fact]
    public async Task LicencaPremium_dto_todos_modulos_habilitados()
    {
        var r = await Roteador(new LicencaFake(TipoLicenca.Premium))
            .TratarAsync(Req("obterstatuslicenca"));

        Assert.True(r.Sucesso);
        var dto = Assert.IsType<StatusLicencaDto>(r.Resultado);

        Assert.Equal("Premium", dto.Tipo);
        Assert.True(dto.ModuloUpgrade);
        Assert.True(dto.ContadorVidaUtil);
        Assert.True(dto.GerenciadorDrivers);
        Assert.True(dto.GuiaBiosIa);
    }

    // ── Serviços Windows ──────────────────────────────────────────────────────

    [Fact]
    public async Task Servicos_pararservico_critico_bloqueado_com_mensagem_descritiva()
    {
        if (!OperatingSystem.IsWindows()) return;

        // "lsass" está na lista negra: parar causaria BSOD imediato
        var r = await Roteador().TratarAsync(Req("pararservico", new { nome = "lsass" }));

        Assert.False(r.Sucesso);
        Assert.Contains("crítico", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Servicos_iniciarservico_sem_nome_retorna_falha_descritiva()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("iniciarservico"));

        Assert.False(r.Sucesso);
        Assert.Contains("nome", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Servicos_alterarmododeinicio_modo_invalido_retorna_falha()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Modo desconhecido deve ser rejeitado ANTES de tentar qualquer PowerShell
        var r = await Roteador().TratarAsync(
            Req("alterarmododeinicio", new { nome = "Spooler", modo = "ModoTotalmenteInvalido" }));

        Assert.False(r.Sucesso);
        Assert.Contains("desconhecido", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Servicos_alterarmododeinicio_sem_modo_retorna_falha()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Parâmetro "modo" ausente → falha de validação
        var r = await Roteador().TratarAsync(
            Req("alterarmododeinicio", new { nome = "Spooler" }));

        Assert.False(r.Sucesso);
        Assert.Contains("modo", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ── Limpeza Windows ───────────────────────────────────────────────────────

    [Fact]
    public async Task Limpeza_executarlimpeza_sem_param_ids_retorna_falha()
    {
        if (!OperatingSystem.IsWindows()) return;

        var r = await Roteador().TratarAsync(Req("executarlimpeza"));

        Assert.False(r.Sucesso);
        Assert.Contains("ids", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ── Fakes locais ──────────────────────────────────────────────────────────

    private sealed class ColetorFake : IColetorInventario
    {
        public Task<Inventario> ColetarAsync(CancellationToken ct = default) =>
            Task.FromResult(new Inventario
            {
                Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806" },
                Cpu   = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6 },
                Gpu   = new[] { new PlacaVideo { Nome = "RTX 3060" } },
                SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
            });
    }

    private sealed class LicencaFake : IServicoLicenca
    {
        private readonly TipoLicenca _tipo;
        public LicencaFake(TipoLicenca tipo) => _tipo = tipo;

        public TipoLicenca TipoAtual                    => _tipo;
        public string?     NomeCliente                  => null;
        public string?     EmailCliente                 => null;
        public bool        TemAcesso(FuncionalidadePremium _) => _tipo == TipoLicenca.Premium;

        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(ResultadoAtivacao.Ok(_tipo));
        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default) =>
            Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
        public Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default) =>
            Task.FromResult(ResultadoAtivacao.Ok(_tipo));
    }
}
