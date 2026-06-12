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
        EntradasStartup = new ObservableCollection<InicializacaoEntradaViewModel>();
    }

    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _statusOtimizador = "Pronto.";
    [ObservableProperty] private bool _efeitosVisuaisDesativados;

    public ObservableCollection<InicializacaoEntradaViewModel> EntradasStartup { get; }

    [RelayCommand]
    private async Task DesativarEfeitosVisuaisAsync()
    {
        Ocupado = true;
        StatusOtimizador = "Desativando efeitos visuais…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo = "aplicar",
                Parametros = JsonSerializer.SerializeToElement(new { acoes = new[] { "SO_EFEITOS_VISUAIS_DESEMPENHO" } }),
            });
            EfeitosVisuaisDesativados = resp.Sucesso;
            StatusOtimizador = resp.Sucesso ? "Efeitos visuais desativados com sucesso." : "Falha: " + resp.Erro;
        }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private async Task VarrerStartupAsync()
    {
        Ocupado = true;
        StatusOtimizador = "Varrendo entradas de inicialização…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "ObterEntradasStartup" });
            EntradasStartup.Clear();
            if (resp.Resultado is IReadOnlyList<InicializacaoEntrada> entradas)
            {
                foreach (var e in entradas)
                    EntradasStartup.Add(new InicializacaoEntradaViewModel(e));
            }
            StatusOtimizador = $"Encontradas {EntradasStartup.Count} entrada(s) de startup.";
        }
        finally { Ocupado = false; }
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
                Metodo = "DesativarStartup",
                Parametros = JsonSerializer.SerializeToElement(new { nome = entrada.Nome }),
            });
            if (resp.Sucesso) entrada.Ativo = false;
        }
        finally { Ocupado = false; }
    }
}

public partial class InicializacaoEntradaViewModel : ObservableObject
{
    private readonly InicializacaoEntrada _modelo;

    public InicializacaoEntradaViewModel(InicializacaoEntrada modelo)
    {
        _modelo = modelo;
        Ativo = modelo.Ativo;
    }

    public string Nome => _modelo.Nome;
    public string Caminho => _modelo.Caminho;
    public string Impacto => _modelo.Impacto.ToString();
    public string Origem => _modelo.Origem.ToString();

    [ObservableProperty] private bool _ativo;

    public string CorImpacto => _modelo.Impacto switch
    {
        ImpactoInicializacao.Alto => "#FF3333",
        ImpactoInicializacao.Medio => "#FFCC00",
        ImpactoInicializacao.Baixo => "#00FF88",
        _ => "#888888",
    };
}
