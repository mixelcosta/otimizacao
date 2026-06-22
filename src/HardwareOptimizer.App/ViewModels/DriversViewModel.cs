using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.ViewModels;

public partial class DriversViewModel : ObservableObject
{
    private IReadOnlyList<InfoDriver> _todosDrivers = [];

    public DriversViewModel()
    {
        Drivers = new ObservableCollection<InfoDriverViewModel>();
    }

    [ObservableProperty] private string _statusText = "Execute o SCAN para listar os drivers instalados.";
    [ObservableProperty] private string _ultimoScan = string.Empty;
    [ObservableProperty] private bool _temResultados;
    [ObservableProperty] private string _filtroTexto = string.Empty;

    partial void OnFiltroTextoChanged(string value) => AplicarFiltro();

    public ObservableCollection<InfoDriverViewModel> Drivers { get; }

    public void Popular(IReadOnlyList<InfoDriver> drivers)
    {
        _todosDrivers = drivers.OrderBy(x => x.Descricao).ToList();
        AplicarFiltro();
        UltimoScan = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
    }

    private void AplicarFiltro()
    {
        Drivers.Clear();

        var fonte = string.IsNullOrWhiteSpace(FiltroTexto)
            ? _todosDrivers
            : _todosDrivers.Where(d =>
                d.Descricao.Contains(FiltroTexto, StringComparison.OrdinalIgnoreCase) ||
                (d.Fabricante?.Contains(FiltroTexto, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var d in fonte)
            Drivers.Add(new InfoDriverViewModel(d));

        TemResultados = Drivers.Count > 0;
        StatusText = _todosDrivers.Count == 0
            ? "Nenhum dispositivo detectado."
            : $"{Drivers.Count} de {_todosDrivers.Count} dispositivo(s).";
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
            StatusDriver.Atualizado            => ("ATUALIZADO",  new SolidColorBrush(Color.Parse("#00FF88")), new SolidColorBrush(Color.Parse("#00FF8815"))),
            StatusDriver.AtualizacaoDisponivel => ("ATUALIZAÇÃO", new SolidColorBrush(Color.Parse("#FFCC00")), new SolidColorBrush(Color.Parse("#FFCC0015"))),
            _                                  => ("—",           new SolidColorBrush(Color.Parse("#484865")), new SolidColorBrush(Color.Parse("#48486510"))),
        };

        CertificadoWhql = d.CertificadoWhql;

        var hwid = d.HardwareId;
        HwidCurto = hwid.Length > 50 ? hwid[..47] + "…" : hwid;
    }

    public string Descricao       { get; }
    public string Fabricante      { get; }
    public string VersaoAtual     { get; }
    public string StatusTexto     { get; }
    public IBrush CorStatus       { get; }
    public IBrush CorFundo        { get; }
    public bool   CertificadoWhql { get; }
    public string HwidCurto       { get; }
}
