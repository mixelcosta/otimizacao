using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class OtimizadorWindowsViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public OtimizadorWindowsViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        EntradasStartup = [];
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

    [ObservableProperty] private bool   _ocupado;
    [ObservableProperty] private string _statusOtimizador       = "Pronto.";
    [ObservableProperty] private bool   _efeitosVisuaisDesativados;

    public ObservableCollection<EfeitoVisualViewModel>      EfeitosVisuais  { get; }
    public ObservableCollection<InicializacaoEntradaViewModel> EntradasStartup { get; }

    // ── Seleção rápida ─────────────────────────────────────────────────────

    [RelayCommand]
    private void SelecionarTudo()
    {
        foreach (var e in EfeitosVisuais) e.Selecionado = true;
    }

    [RelayCommand]
    private void LimparSelecao()
    {
        foreach (var e in EfeitosVisuais) e.Selecionado = false;
    }

    // ── Aplicar efeitos selecionados ───────────────────────────────────────

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

    // ── Startup scanner ────────────────────────────────────────────────────

    public void Popular(IReadOnlyList<InicializacaoEntrada> entradas)
    {
        EntradasStartup.Clear();
        foreach (var e in entradas)
            EntradasStartup.Add(new InicializacaoEntradaViewModel(e));
        StatusOtimizador = $"Encontradas {EntradasStartup.Count} entrada(s) de startup.";
    }

    [RelayCommand]
    private async Task DesativarEntradaAsync(InicializacaoEntradaViewModel entrada)
    {
        if (entrada is null) return;
        Ocupado = true;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo     = "DesativarStartup",
                Parametros = JsonSerializer.SerializeToElement(new { nome = entrada.Nome }),
            });
            if (resp.Sucesso) entrada.Ativo = false;
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

public partial class InicializacaoEntradaViewModel : ObservableObject
{
    private readonly InicializacaoEntrada _modelo;

    public InicializacaoEntradaViewModel(InicializacaoEntrada modelo)
    {
        _modelo = modelo;
        Ativo   = modelo.Ativo;
    }

    public string Nome    => _modelo.Nome;
    public string Caminho => _modelo.Caminho;
    public string Impacto => _modelo.Impacto.ToString();
    public string Origem  => _modelo.Origem.ToString();

    [ObservableProperty] private bool _ativo;

    public string CorImpacto => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto  => "#FF3333",
        ImpactoInicializacao.Medio => "#FFCC00",
        ImpactoInicializacao.Baixo => "#00FF88",
        _                          => "#888888",
    };
}
