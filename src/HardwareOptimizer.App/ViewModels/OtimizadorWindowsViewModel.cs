using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public enum SubPaginaOtimizador { EfeitosVisuais, ProgramasInstalados, Inicializacao }

public partial class OtimizadorWindowsViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private List<ProgramaInstaladoViewModel> _todosProgramas = [];

    public OtimizadorWindowsViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        EntradasStartup = [];
        ProgramasFiltrados = [];
        EfeitosVisuais =
        [
            new("Animações ao minimizar e maximizar janelas"),
            new("Animações na barra de tarefas e área de notificação"),
            new("Transparência e vidro (Aero / DWM compositing)"),
            new("Sombras sob janelas e cursor do mouse"),
            new("Mostrar conteúdo das janelas ao arrastar"),
            new("Efeito Peek — pré-visualização na área de trabalho"),
            new("Fade e deslizamento de menus e dicas de ferramenta"),
            new("Miniaturas de janelas na barra de tarefas (Aero Snap)"),
            new("Suavização de bordas de fontes de tela (ClearType sub-pixel)"),
        ];
    }

    // ── Submenu ────────────────────────────────────────────────────────────

    [ObservableProperty] private SubPaginaOtimizador _subPagina = SubPaginaOtimizador.EfeitosVisuais;

    partial void OnSubPaginaChanged(SubPaginaOtimizador _)
    {
        OnPropertyChanged(nameof(MostrarEfeitosVisuais));
        OnPropertyChanged(nameof(MostrarProgramas));
        OnPropertyChanged(nameof(MostrarInicializacao));
        OnPropertyChanged(nameof(AbaEfeitosAtiva));
        OnPropertyChanged(nameof(AbaProgramasAtiva));
        OnPropertyChanged(nameof(AbaInicializacaoAtiva));
    }

    public bool MostrarEfeitosVisuais => SubPagina == SubPaginaOtimizador.EfeitosVisuais;
    public bool MostrarProgramas      => SubPagina == SubPaginaOtimizador.ProgramasInstalados;
    public bool MostrarInicializacao  => SubPagina == SubPaginaOtimizador.Inicializacao;
    public bool AbaEfeitosAtiva       => SubPagina == SubPaginaOtimizador.EfeitosVisuais;
    public bool AbaProgramasAtiva     => SubPagina == SubPaginaOtimizador.ProgramasInstalados;
    public bool AbaInicializacaoAtiva => SubPagina == SubPaginaOtimizador.Inicializacao;

    [RelayCommand] private void IrParaEfeitosVisuais() => SubPagina = SubPaginaOtimizador.EfeitosVisuais;
    [RelayCommand] private void IrParaProgramas()      => SubPagina = SubPaginaOtimizador.ProgramasInstalados;
    [RelayCommand] private void IrParaInicializacao()  => SubPagina = SubPaginaOtimizador.Inicializacao;

    // ── Estado geral ───────────────────────────────────────────────────────

    [ObservableProperty] private bool   _ocupado;
    [ObservableProperty] private string _statusOtimizador       = "Pronto.";
    [ObservableProperty] private bool   _efeitosVisuaisDesativados;

    // ── Efeitos Visuais ────────────────────────────────────────────────────

    public ObservableCollection<EfeitoVisualViewModel> EfeitosVisuais { get; }

    [RelayCommand]
    private void SelecionarTudo() { foreach (var e in EfeitosVisuais) e.Selecionado = true; }

    [RelayCommand]
    private void LimparSelecao() { foreach (var e in EfeitosVisuais) e.Selecionado = false; }

    [RelayCommand]
    private async Task AplicarEfeitosSelecionadosAsync()
    {
        var selecionados = EfeitosVisuais.Where(e => e.Selecionado).ToList();
        if (selecionados.Count == 0)
        {
            StatusOtimizador = "Nenhum efeito selecionado.";
            return;
        }

        Ocupado = true;
        StatusOtimizador = $"Desativando {selecionados.Count} efeito(s)…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = "aplicar",
                Parametros = JsonSerializer.SerializeToElement(
                    new { acoes = new[] { "SO_EFEITOS_VISUAIS_DESEMPENHO" } }),
            });
            EfeitosVisuaisDesativados = resp.Sucesso;
            StatusOtimizador = resp.Sucesso
                ? $"{selecionados.Count} efeito(s) desativado(s) com sucesso."
                : "Falha: " + resp.Erro;
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
}

// ── ViewModels auxiliares ──────────────────────────────────────────────────

public partial class EfeitoVisualViewModel : ObservableObject
{
    public string Nome { get; }
    [ObservableProperty] private bool _selecionado = true;
    public EfeitoVisualViewModel(string nome) => Nome = nome;
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

    public string Nome   => _modelo.Nome;
    public string Caminho => _modelo.Caminho;

    public string Origem => _modelo.Origem switch
    {
        OrigemInicializacao.RegistroUsuario => "Usuário",
        OrigemInicializacao.RegistroMaquina => "Sistema",
        OrigemInicializacao.PastaStartup    => "Pasta Startup",
        _ => _modelo.Origem.ToString(),
    };

    public string TextoImpacto => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto       => "Alto impacto",
        ImpactoInicializacao.Medio      => "Médio impacto",
        ImpactoInicializacao.Baixo      => "Baixo impacto",
        ImpactoInicializacao.Desconhecido => "Impacto desconhecido",
        _ => "—",
    };

    public string CorImpacto => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto       => "#FF4444",
        ImpactoInicializacao.Medio      => "#FFCC00",
        ImpactoInicializacao.Baixo      => "#00FF88",
        _ => "#555555",
    };

    public string CorImpactoFundo => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto       => "#2A0808",
        ImpactoInicializacao.Medio      => "#2A2200",
        ImpactoInicializacao.Baixo      => "#082A12",
        _ => "#1A1A1A",
    };

    [ObservableProperty] private bool _ativo;

    partial void OnAtivoChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoBotao));
        OnPropertyChanged(nameof(CorBotaoFundo));
        OnPropertyChanged(nameof(CorBotaoTexto));
        OnPropertyChanged(nameof(CorIndicador));
    }

    public string TextoBotao    => Ativo ? "DESATIVAR" : "ATIVAR";
    public string CorBotaoFundo => Ativo ? "#2A0808"   : "#082A12";
    public string CorBotaoTexto => Ativo ? "#CC3333"   : "#00C870";
    public string CorIndicador  => Ativo ? "#00FF88"   : "#252525";
}
