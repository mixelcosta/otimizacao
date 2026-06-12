using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class BiosGuideViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private readonly GeradorGuiaXmpExpo _gerador = new();

    public BiosGuideViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        PassosXmp = new ObservableCollection<PassoGuiaViewModel>();
    }

    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _fabricantePlaca = "–";
    [ObservableProperty] private string _modeloPlaca = "–";
    [ObservableProperty] private string _teclaSetup = "–";
    [ObservableProperty] private string _avisoXmp = string.Empty;
    [ObservableProperty] private int _passoAtual;

    public string PassoAtualInstrucao =>
        PassoAtual > 0 && PassoAtual <= PassosXmp.Count
            ? PassosXmp[PassoAtual - 1].Instrucao
            : string.Empty;

    public bool TemPassos => PassosXmp.Count > 0;

    public ObservableCollection<PassoGuiaViewModel> PassosXmp { get; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Ocupado = true;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" });
            if (resp.Sucesso && resp.Resultado is Inventario inv)
            {
                var id = IdentificacaoBios.DeInventario(inv);
                FabricantePlaca = id.Fabricante;
                ModeloPlaca = id.Modelo;

                var tipoRam = inv.Memoria.FirstOrDefault()?.VelocidadeMhz >= 4800 ? "DDR5" : "DDR4";
                var guia = _gerador.Gerar(id, tipoRam);

                TeclaSetup = guia.TeclaSetup;
                AvisoXmp = guia.Aviso;

                PassosXmp.Clear();
                for (int i = 0; i < guia.Passos.Count; i++)
                    PassosXmp.Add(new PassoGuiaViewModel(i + 1, guia.Passos[i]));

                PassoAtual = 1;
                OnPropertyChanged(nameof(PassoAtualInstrucao));
                OnPropertyChanged(nameof(TemPassos));
            }
        }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private void ProximoPasso()
    {
        if (PassoAtual < PassosXmp.Count)
        {
            PassoAtual++;
            OnPropertyChanged(nameof(PassoAtualInstrucao));
        }
    }

    [RelayCommand]
    private void PassoAnterior()
    {
        if (PassoAtual > 1)
        {
            PassoAtual--;
            OnPropertyChanged(nameof(PassoAtualInstrucao));
        }
    }
}

public sealed record PassoGuiaViewModel(int Numero, string Instrucao);
