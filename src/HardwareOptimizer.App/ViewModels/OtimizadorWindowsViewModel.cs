using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using System.Linq;

namespace HardwareOptimizer.App.ViewModels;

public enum SubPaginaOtimizador { EfeitosVisuais, ProgramasInstalados, Inicializacao, Servicos }

public partial class OtimizadorWindowsViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private List<ProgramaInstaladoViewModel> _todosProgramas = [];

    private List<ServicoViewModel> _todosServicos = [];
    private bool _servicosCarregados;

    public OtimizadorWindowsViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        EntradasStartup = [];
        ProgramasFiltrados = [];
        ServicosFiltrados = [];
        EfeitosVisuais = [];
        _ = CarregarEfeitosVisuaisAsync();
    }

    // ── Submenu ────────────────────────────────────────────────────────────

    [ObservableProperty] private SubPaginaOtimizador _subPagina = SubPaginaOtimizador.EfeitosVisuais;

    partial void OnSubPaginaChanged(SubPaginaOtimizador value)
    {
        OnPropertyChanged(nameof(MostrarEfeitosVisuais));
        OnPropertyChanged(nameof(MostrarProgramas));
        OnPropertyChanged(nameof(MostrarInicializacao));
        OnPropertyChanged(nameof(MostrarServicos));
        OnPropertyChanged(nameof(AbaEfeitosAtiva));
        OnPropertyChanged(nameof(AbaProgramasAtiva));
        OnPropertyChanged(nameof(AbaInicializacaoAtiva));
        OnPropertyChanged(nameof(AbaServicosAtiva));

        if (value == SubPaginaOtimizador.EfeitosVisuais)
            _ = CarregarEfeitosVisuaisAsync();
        else if (value == SubPaginaOtimizador.Servicos && !_servicosCarregados)
            _ = CarregarServicosAsync();
    }

    public bool MostrarEfeitosVisuais => SubPagina == SubPaginaOtimizador.EfeitosVisuais;
    public bool MostrarProgramas      => SubPagina == SubPaginaOtimizador.ProgramasInstalados;
    public bool MostrarInicializacao  => SubPagina == SubPaginaOtimizador.Inicializacao;
    public bool MostrarServicos       => SubPagina == SubPaginaOtimizador.Servicos;
    public bool AbaEfeitosAtiva       => SubPagina == SubPaginaOtimizador.EfeitosVisuais;
    public bool AbaProgramasAtiva     => SubPagina == SubPaginaOtimizador.ProgramasInstalados;
    public bool AbaInicializacaoAtiva => SubPagina == SubPaginaOtimizador.Inicializacao;
    public bool AbaServicosAtiva      => SubPagina == SubPaginaOtimizador.Servicos;

    [RelayCommand] private void IrParaEfeitosVisuais() => SubPagina = SubPaginaOtimizador.EfeitosVisuais;
    [RelayCommand] private void IrParaProgramas()      => SubPagina = SubPaginaOtimizador.ProgramasInstalados;
    [RelayCommand] private void IrParaInicializacao()  => SubPagina = SubPaginaOtimizador.Inicializacao;
    [RelayCommand] private void IrParaServicos()       => SubPagina = SubPaginaOtimizador.Servicos;

    // ── Estado geral ───────────────────────────────────────────────────────

    [ObservableProperty] private bool   _ocupado;
    [ObservableProperty] private string _statusOtimizador       = "Pronto.";
    [ObservableProperty] private bool   _efeitosVisuaisDesativados;

    // ── Efeitos Visuais ────────────────────────────────────────────────────

    [ObservableProperty] private bool _carregandoEfeitos;

    public ObservableCollection<EfeitoVisualViewModel> EfeitosVisuais { get; }

    [RelayCommand]
    private async Task CarregarEfeitosVisuaisAsync()
    {
        CarregandoEfeitos = true;
        StatusOtimizador = "Lendo configurações do Windows…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "obterefeitosvisuais" });
            if (!resp.Sucesso)
            {
                StatusOtimizador = "Falha ao ler efeitos: " + resp.Erro;
                return;
            }

            if (resp.Resultado is not IReadOnlyList<EfeitoVisual> lista) return;

            EfeitosVisuais.Clear();
            foreach (var e in lista)
                EfeitosVisuais.Add(new EfeitoVisualViewModel(e, ToggleEfeitoAsync));

            EfeitosVisuaisDesativados = EfeitosVisuais.All(e => !e.Ativo);
            StatusOtimizador = "Pronto.";
        }
        finally { CarregandoEfeitos = false; }
    }

    private async Task ToggleEfeitoAsync(EfeitoVisualViewModel efeito)
    {
        Ocupado = true;
        var novoEstado = !efeito.Ativo;
        var acao = novoEstado ? "Ativando" : "Desativando";
        StatusOtimizador = acao + " " + efeito.Nome + "...";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = "alterarefeito",
                Parametros = JsonSerializer.SerializeToElement(new { id = efeito.Id, ativo = novoEstado }),
            });

            if (resp.Sucesso)
            {
                efeito.Ativo = novoEstado;
                EfeitosVisuaisDesativados = EfeitosVisuais.All(e => !e.Ativo);
                var resultado = novoEstado ? "ativado" : "desativado";
                StatusOtimizador = efeito.Nome + " " + resultado + ".";
            }
            else
            {
                StatusOtimizador = "Falha: " + resp.Erro;
            }
        }
        finally { Ocupado = false; }
    }

    // ── Programas Instalados ───────────────────────────────────────────────

    [ObservableProperty] private string _filtroProgramas     = "";
    [ObservableProperty] private string _totalProgramasLabel = "—";
    [ObservableProperty] private string _desinstalarLabel    = "Desinstalar selecionados";

    public ObservableCollection<ProgramaInstaladoViewModel> ProgramasFiltrados { get; }

    partial void OnFiltroProgramasChanged(string value) => AplicarFiltro();

    public void PopularProgramas(IReadOnlyList<ProgramaInstalado> lista)
    {
        foreach (var p in _todosProgramas)
            p.PropertyChanged -= OnProgramaPropertyChanged;

        _todosProgramas = lista
            .OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ProgramaInstaladoViewModel(p))
            .ToList();

        foreach (var p in _todosProgramas)
            p.PropertyChanged += OnProgramaPropertyChanged;

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var filtro = FiltroProgramas.Trim();
        var resultado = string.IsNullOrEmpty(filtro)
            ? _todosProgramas
            : _todosProgramas.Where(p =>
                p.Nome.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                p.Fabricante.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

        ProgramasFiltrados.Clear();
        foreach (var p in resultado) ProgramasFiltrados.Add(p);

        int n = resultado.Count;
        TotalProgramasLabel = n == 0
            ? "Nenhum programa encontrado"
            : $"{n} programa{(n != 1 ? "s" : "")} encontrado{(n != 1 ? "s" : "")}";

        AtualizarDesinstalarLabel();
    }

    private void OnProgramaPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProgramaInstaladoViewModel.Selecionado))
            AtualizarDesinstalarLabel();
    }

    private void AtualizarDesinstalarLabel()
    {
        int n = _todosProgramas.Count(p => p.Selecionado);
        DesinstalarLabel = n == 0
            ? "Desinstalar selecionados"
            : $"Desinstalar {n} selecionado{(n != 1 ? "s" : "")}";
    }

    [RelayCommand]
    private async Task DesinstalarSelecionadosAsync()
    {
        var selecionados = _todosProgramas.Where(p => p.Selecionado).ToList();
        if (selecionados.Count == 0)
        {
            StatusOtimizador = "Nenhum programa selecionado.";
            return;
        }

        Ocupado = true;
        StatusOtimizador = $"Iniciando desinstalação de {selecionados.Count} programa(s)…";
        try
        {
            var payload = selecionados.Select(p => new
            {
                nome                 = p.Nome,
                uninstallString      = p.UninstallString,
                quietUninstallString = p.QuietUninstallString,
            });

            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = "DesinstalarProgramas",
                Parametros = JsonSerializer.SerializeToElement(new { programas = payload }),
            });

            StatusOtimizador = resp.Sucesso
                ? $"{selecionados.Count} desinstalador(es) iniciado(s). Conclua cada janela aberta."
                : "Falha: " + resp.Erro;
        }
        finally { Ocupado = false; }
    }

    // ── Inicialização do Windows ───────────────────────────────────────────

    public ObservableCollection<InicializacaoEntradaViewModel> EntradasStartup { get; }

    public void Popular(IReadOnlyList<InicializacaoEntrada> entradas)
    {
        EntradasStartup.Clear();

        var ordenadas = entradas.OrderBy(e => e.Impacto switch
        {
            ImpactoInicializacao.Alto       => 0,
            ImpactoInicializacao.Medio      => 1,
            ImpactoInicializacao.Baixo      => 2,
            ImpactoInicializacao.Desconhecido => 3,
            _ => 4,
        }).ThenBy(e => e.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var e in ordenadas)
            EntradasStartup.Add(new InicializacaoEntradaViewModel(e, ToggleEntradaAsync));
    }

    private async Task ToggleEntradaAsync(InicializacaoEntradaViewModel entrada)
    {
        Ocupado = true;
        try
        {
            var metodo = entrada.Ativo ? "DesativarStartup" : "AtivarStartup";
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = metodo,
                Parametros = JsonSerializer.SerializeToElement(new { nome = entrada.Nome }),
            });

            if (resp.Sucesso)
                entrada.Ativo = !entrada.Ativo;
            else
                StatusOtimizador = "Falha: " + resp.Erro;
        }
        finally { Ocupado = false; }
    }

    // ── Serviços Windows ───────────────────────────────────────────────────

    [ObservableProperty] private string _filtroServicos      = "";
    [ObservableProperty] private string _totalServicosLabel  = "—";
    [ObservableProperty] private bool   _carregandoServicos;

    public ObservableCollection<ServicoViewModel> ServicosFiltrados { get; }

    partial void OnFiltroServicosChanged(string value) => AplicarFiltroServicos();

    [RelayCommand]
    private async Task CarregarServicosAsync()
    {
        CarregandoServicos = true;
        StatusOtimizador   = "Carregando serviços…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "obterservicos" });
            if (!resp.Sucesso) { StatusOtimizador = "Falha: " + resp.Erro; return; }

            if (resp.Resultado is not IReadOnlyList<ServicoWindows> lista) return;
            _todosServicos = lista
                .Select(s => new ServicoViewModel(s, ToggleServicoAsync, AlterarModoServicoAsync))
                .ToList();
            _servicosCarregados = true;
            AplicarFiltroServicos();
            StatusOtimizador = "Pronto.";
        }
        finally { CarregandoServicos = false; }
    }

    private void AplicarFiltroServicos()
    {
        var filtro = FiltroServicos.Trim();
        var resultado = string.IsNullOrEmpty(filtro)
            ? _todosServicos
            : _todosServicos.Where(s =>
                s.Nome.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                s.Descricao.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

        ServicosFiltrados.Clear();
        foreach (var s in resultado) ServicosFiltrados.Add(s);

        int n = resultado.Count;
        TotalServicosLabel = n == 0
            ? "Nenhum serviço encontrado"
            : $"{n} serviço{(n != 1 ? "s" : "")} encontrado{(n != 1 ? "s" : "")}";
    }

    private async Task ToggleServicoAsync(ServicoViewModel svc)
    {
        Ocupado = true;
        var iniciar = svc.Status != "Running";
        StatusOtimizador = $"{(iniciar ? "Iniciando" : "Parando")} '{svc.Nome}'…";
        try
        {
            var metodo = iniciar ? "iniciarservico" : "pararservico";
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = metodo,
                Parametros = JsonSerializer.SerializeToElement(new { nome = svc.Nome }),
            });

            if (resp.Sucesso)
            {
                svc.Status = iniciar ? "Running" : "Stopped";
                StatusOtimizador = $"'{svc.Nome}' {(iniciar ? "iniciado" : "parado")} com sucesso.";
            }
            else
            {
                StatusOtimizador = "Falha: " + resp.Erro;
            }
        }
        finally { Ocupado = false; }
    }

    private async Task AlterarModoServicoAsync(ServicoViewModel svc, string novoModo)
    {
        Ocupado = true;
        StatusOtimizador = $"Alterando modo de início de '{svc.Nome}' para '{novoModo}'…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = "alterarmododeinicio",
                Parametros = JsonSerializer.SerializeToElement(new { nome = svc.Nome, modo = novoModo }),
            });

            StatusOtimizador = resp.Sucesso
                ? $"'{svc.Nome}': tipo de início alterado para '{novoModo}'."
                : "Falha: " + resp.Erro;
        }
        finally { Ocupado = false; }
    }
}

// ── ViewModels auxiliares ──────────────────────────────────────────────────

public partial class EfeitoVisualViewModel : ObservableObject
{
    private readonly Func<EfeitoVisualViewModel, Task> _toggle;

    public EfeitoVisualViewModel(EfeitoVisual modelo, Func<EfeitoVisualViewModel, Task> toggle)
    {
        Id     = modelo.Id;
        Nome   = modelo.Nome;
        _ativo = modelo.Ativo;
        _toggle = toggle;
        ToggleCommand = new AsyncRelayCommand(() => _toggle(this));
    }

    public IAsyncRelayCommand ToggleCommand { get; }

    public string Id   { get; }
    public string Nome { get; }

    [ObservableProperty]
    private bool _ativo;

    partial void OnAtivoChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoBotao));
        OnPropertyChanged(nameof(CorBotaoFundo));
        OnPropertyChanged(nameof(CorBotaoTexto));
        OnPropertyChanged(nameof(CorIndicador));
        OnPropertyChanged(nameof(CorNome));
    }

    public string TextoBotao    => Ativo ? "DESATIVAR" : "ATIVAR";
    public string CorBotaoFundo => Ativo ? "#2A0808"   : "#082A12";
    public string CorBotaoTexto => Ativo ? "#CC3333"   : "#00C870";
    public string CorIndicador  => Ativo ? "#00C870"   : "#282840";
    public string CorNome       => Ativo ? "#E0E0F2"   : "#484865";
}

public partial class ProgramaInstaladoViewModel : ObservableObject
{
    public string  Nome                 { get; }
    public string  Fabricante           { get; }
    public string  Versao               { get; }
    public string  Tamanho              { get; }
    public string  Data                 { get; }
    public bool    Bloatware            { get; }
    public string? UninstallString      { get; }
    public string? QuietUninstallString { get; }

    [ObservableProperty] private bool _selecionado;

    public ProgramaInstaladoViewModel(ProgramaInstalado p)
    {
        Nome                 = p.Nome;
        Fabricante           = p.Fabricante ?? "—";
        Versao               = p.Versao ?? "—";
        Bloatware            = p.Bloatware;
        UninstallString      = p.UninstallString;
        QuietUninstallString = p.QuietUninstallString;

        Tamanho = p.TamanhoMb is > 0
            ? p.TamanhoMb >= 1024
                ? $"{p.TamanhoMb / 1024.0:F1} GB"
                : $"{p.TamanhoMb} MB"
            : "";

        Data = p.DataInstalacao is { Length: 8 } d
            ? $"{d[6..8]}/{d[4..6]}/{d[0..4]}"
            : "";
    }
}

public partial class ServicoViewModel : ObservableObject
{
    private readonly ServicoWindows _modelo;
    private readonly Func<ServicoViewModel, string, Task> _alterarModoCallback;
    private bool _inicializado;

    public static readonly IReadOnlyList<string> ModoInicioOpcoes =
        ["Automático", "Automático (Atraso na Inicialização)", "Manual", "Desativado"];

    public ServicoViewModel(
        ServicoWindows modelo,
        Func<ServicoViewModel, Task> toggle,
        Func<ServicoViewModel, string, Task> alterarModo)
    {
        _modelo = modelo;
        _alterarModoCallback = alterarModo;
        _status = modelo.Status;
        _modoInicioSelecionado = ConverterModoInicio(modelo.ModoInicio);
        ToggleCommand = new AsyncRelayCommand(() => toggle(this));
        _inicializado = true;
    }

    public IAsyncRelayCommand ToggleCommand { get; }

    public string  Nome     => _modelo.Nome;
    public string  Descricao => _modelo.Descricao;
    public int     Pid      => _modelo.Pid;

    [ObservableProperty] private string _status;
    [ObservableProperty] private string _modoInicioSelecionado;

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(TextoStatus));
        OnPropertyChanged(nameof(CorStatus));
        OnPropertyChanged(nameof(TextoBotao));
        OnPropertyChanged(nameof(CorBotaoFundo));
        OnPropertyChanged(nameof(CorBotaoTexto));
        OnPropertyChanged(nameof(PidTexto));
        OnPropertyChanged(nameof(Rodando));
    }

    partial void OnModoInicioSelecionadoChanged(string value)
    {
        if (_inicializado)
            _ = _alterarModoCallback(this, value);
    }

    public bool   Rodando      => Status == "Running";
    public string TextoStatus  => Status == "Running" ? "Em execução" : Status == "Stopped" ? "Parado" : Status;
    public string CorStatus    => Status == "Running" ? "#00C870" : "#555555";
    public string TextoBotao   => Status == "Running" ? "PARAR" : "INICIAR";
    public string CorBotaoFundo => Status == "Running" ? "#2A0808" : "#082A12";
    public string CorBotaoTexto => Status == "Running" ? "#CC3333" : "#00C870";
    public string PidTexto     => Pid > 0 ? Pid.ToString() : "—";

    private static string ConverterModoInicio(string? wmiMode) => wmiMode switch
    {
        "Auto"     => "Automático",
        "Manual"   => "Manual",
        "Disabled" => "Desativado",
        _          => "Manual",
    };
}

public partial class InicializacaoEntradaViewModel : ObservableObject
{
    private readonly InicializacaoEntrada _modelo;

    public InicializacaoEntradaViewModel(InicializacaoEntrada modelo, Func<InicializacaoEntradaViewModel, Task> toggle)
    {
        _modelo = modelo;
        Ativo   = modelo.Ativo;
        ToggleCommand = new AsyncRelayCommand(() => toggle(this));
    }

    public IAsyncRelayCommand ToggleCommand { get; }

    public string  Nome      => _modelo.Nome;
    public string  Caminho   => _modelo.Caminho;
    public string  Fabricante => _modelo.Fabricante ?? "—";

    public string Origem => _modelo.Origem switch
    {
        OrigemInicializacao.RegistroUsuario => "Usuário",
        OrigemInicializacao.RegistroMaquina => "Sistema",
        OrigemInicializacao.PastaStartup    => "Pasta Startup",
        _ => _modelo.Origem.ToString(),
    };

    public string TextoImpacto => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto  => "Alto",
        ImpactoInicializacao.Medio => "Médio",
        ImpactoInicializacao.Baixo => "Baixo",
        _ => "Nenhum",
    };

    public string CorImpacto => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto  => "#FF4444",
        ImpactoInicializacao.Medio => "#FFCC00",
        ImpactoInicializacao.Baixo => "#00FF88",
        _ => "#555555",
    };

    [ObservableProperty] private bool _ativo;

    partial void OnAtivoChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoStatus));
        OnPropertyChanged(nameof(CorStatus));
        OnPropertyChanged(nameof(CorBotaoFundo));
        OnPropertyChanged(nameof(CorBotaoTexto));
    }

    public string TextoStatus   => Ativo ? "Habilitado"   : "Desabilitado";
    public string CorStatus     => Ativo ? "#00C870"      : "#555555";
    public string CorBotaoFundo => Ativo ? "#2A0808"      : "#082A12";
    public string CorBotaoTexto => Ativo ? "#CC3333"      : "#00C870";
}
