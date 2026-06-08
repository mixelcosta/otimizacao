using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

/// <summary>
/// ViewModel principal. Orquestra a UI consumindo o agente pelo contrato
/// <see cref="IRoteadorIpc"/> — testável com um roteador falso. Cada ação
/// corresponde a um método do protocolo IPC.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public MainWindowViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        Sensores = new ObservableCollection<string>();
        Matriz = new ObservableCollection<ItemMatrizViewModel>();
    }

    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    private string _status = "Pronto.";

    [ObservableProperty]
    private string _inventarioResumo = "(inventário não coletado)";

    [ObservableProperty]
    private string _resultadoAprovacao = string.Empty;

    public ObservableCollection<string> Sensores { get; }

    public ObservableCollection<ItemMatrizViewModel> Matriz { get; }

    [RelayCommand]
    private Task Coletar() => ExecutarAsync(new RequisicaoIpc { Metodo = "coletar" }, resposta =>
    {
        if (resposta.Resultado is Inventario inv)
        {
            InventarioResumo =
                $"{inv.Placa.Fabricante} {inv.Placa.Modelo} • {inv.Cpu.Nome} • {inv.SistemaOperacional.Nome}";
        }
    });

    [RelayCommand]
    private Task LerSensores() => ExecutarAsync(new RequisicaoIpc { Metodo = "sensores" }, resposta =>
    {
        Sensores.Clear();
        if (resposta.Resultado is LeituraSensores leitura)
        {
            foreach (var sensor in leitura.Sensores)
            {
                Sensores.Add($"{sensor.Tipo} — {sensor.Nome}: {sensor.Valor} {sensor.Unidade}");
            }
        }

        if (Sensores.Count == 0)
        {
            Sensores.Add("(nenhum sensor legível nesta máquina)");
        }
    });

    [RelayCommand]
    private Task Propor() => ExecutarAsync(new RequisicaoIpc { Metodo = "proposta" }, resposta =>
    {
        Matriz.Clear();
        if (resposta.Resultado is MatrizDecisao matriz)
        {
            foreach (var item in matriz.Itens)
            {
                Matriz.Add(new ItemMatrizViewModel(item));
            }
        }
    });

    [RelayCommand]
    private async Task Aprovar()
    {
        var selecionadas = Matriz.Where(i => i.Selecionado).Select(i => i.AcaoId).ToArray();
        if (selecionadas.Length == 0)
        {
            Status = "Selecione ao menos uma ação para aprovar.";
            return;
        }

        var requisicao = new RequisicaoIpc
        {
            Metodo = "aprovar",
            Parametros = JsonSerializer.SerializeToElement(new { acoes = selecionadas }),
        };

        await ExecutarAsync(requisicao, resposta =>
        {
            ResultadoAprovacao = resposta.Sucesso
                ? (resposta.Resultado is RelatorioExecucao r
                    ? $"Aplicado: sucesso={r.Sucesso}, {r.Categorias.Count} categoria(s)."
                    : "Aplicado.")
                : "Falha: " + resposta.Erro;
        });
    }

    private async Task ExecutarAsync(RequisicaoIpc requisicao, Action<RespostaIpc> aoConcluir)
    {
        Ocupado = true;
        Status = $"Executando '{requisicao.Metodo}'…";
        try
        {
            var resposta = await _agente.TratarAsync(requisicao);
            aoConcluir(resposta);
            Status = resposta.Sucesso ? $"'{requisicao.Metodo}' concluído." : $"'{requisicao.Metodo}' falhou: {resposta.Erro}";
        }
        catch (Exception ex)
        {
            Status = "Erro: " + ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }
}
