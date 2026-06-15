using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.ViewModels;

public partial class DriversViewModel : ObservableObject
{
    public DriversViewModel()
    {
        Drivers = new ObservableCollection<InfoDriverViewModel>();
    }

    [ObservableProperty] private string _statusText = "Execute o SCAN para listar os drivers instalados.";
    [ObservableProperty] private string _ultimoScan = string.Empty;
    [ObservableProperty] private bool _temResultados;

    public ObservableCollection<InfoDriverViewModel> Drivers { get; }

    public void Popular(IReadOnlyList<InfoDriver> drivers)
    {
        Drivers.Clear();
        foreach (var d in drivers.OrderBy(x => x.Descricao))
            Drivers.Add(new InfoDriverViewModel(d));

        TemResultados = Drivers.Count > 0;
        StatusText = Drivers.Count > 0
            ? $"{Drivers.Count} dispositivo(s) encontrado(s)."
            : "Nenhum dispositivo detectado.";
        UltimoScan = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
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

        var hwid = d.HardwareId;
        HwidCurto = hwid.Length > 50 ? hwid[..47] + "…" : hwid;
    }

    public string Descricao        { get; }
    public string Fabricante       { get; }
    public string VersaoAtual      { get; }
    public string StatusTexto      { get; }
    public IBrush CorStatus        { get; }
    public IBrush CorFundo         { get; }
    public bool   CertificadoWhql  { get; }
    public string HwidCurto        { get; }
}
