using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class DriversViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public DriversViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        Drivers = new ObservableCollection<InfoDriverViewModel>();
    }

    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _statusText = "Clique em \"Varrer Dispositivos\" para listar os drivers instalados.";
    [ObservableProperty] private string _ultimoScan = string.Empty;
    [ObservableProperty] private bool _temResultados;

    public ObservableCollection<InfoDriverViewModel> Drivers { get; }

    [RelayCommand]
    private async Task VarrerAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        StatusText = "Coletando hardware IDs via WMI…";
        Drivers.Clear();
        TemResultados = false;

        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "obterdrivers" });

            if (resp.Sucesso && resp.Resultado is IReadOnlyList<InfoDriver> lista)
            {
                foreach (var d in lista.OrderBy(x => x.Descricao))
                    Drivers.Add(new InfoDriverViewModel(d));

                TemResultados = Drivers.Count > 0;
                StatusText = $"{Drivers.Count} dispositivo(s) encontrado(s).";
            }
            else
            {
                StatusText = "Não foi possível coletar drivers. Verifique as permissões WMI.";
            }

            UltimoScan = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
        }
        finally { Ocupado = false; }
    }
}

public sealed class InfoDriverViewModel
{
    public InfoDriverViewModel(InfoDriver d)
    {
        Descricao = d.Descricao;
        Fabricante = string.IsNullOrWhiteSpace(d.Fabricante) ? "—" : d.Fabricante;
        VersaoAtual = string.IsNullOrWhiteSpace(d.VersaoAtual) ? "—" : d.VersaoAtual;

        (StatusTexto, CorStatus, CorFundo) = d.Status switch
        {
            StatusDriver.Atualizado            => ("ATUALIZADO",   new SolidColorBrush(Color.Parse("#00FF88")), new SolidColorBrush(Color.Parse("#00FF8815"))),
            StatusDriver.AtualizacaoDisponivel => ("ATUALIZAÇÃO",  new SolidColorBrush(Color.Parse("#FFCC00")), new SolidColorBrush(Color.Parse("#FFCC0015"))),
            _                                  => ("—",            new SolidColorBrush(Color.Parse("#555555")), new SolidColorBrush(Color.Parse("#55555510"))),
        };

        CertificadoWhql = d.CertificadoWhql;

        // Exibe a parte mais legível do HWID
        var hwid = d.HardwareId;
        HwidCurto = hwid.Length > 50 ? hwid[..47] + "…" : hwid;
    }

    public string Descricao     { get; }
    public string Fabricante    { get; }
    public string VersaoAtual   { get; }
    public string StatusTexto   { get; }
    public IBrush CorStatus     { get; }
    public IBrush CorFundo      { get; }
    public bool   CertificadoWhql { get; }
    public string HwidCurto     { get; }
}
