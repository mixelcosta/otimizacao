using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.ViewModels;

public partial class VidaUtilViewModel : ObservableObject
{
    public VidaUtilViewModel()
    {
        Discos = new ObservableCollection<SaudeDiscoViewModel>();
    }

    [ObservableProperty] private string _statusText = "Execute o SCAN para ler os dados S.M.A.R.T.";
    [ObservableProperty] private string _ultimoScan = string.Empty;
    [ObservableProperty] private bool _temResultados;

    public ObservableCollection<SaudeDiscoViewModel> Discos { get; }

    public void Popular(IReadOnlyList<SaudeDisco> discos)
    {
        Discos.Clear();
        foreach (var d in discos)
            Discos.Add(new SaudeDiscoViewModel(d));

        TemResultados = Discos.Count > 0;
        StatusText = Discos.Count > 0
            ? $"{Discos.Count} disco(s) verificado(s)."
            : "Nenhum disco S.M.A.R.T. encontrado neste sistema.";
        UltimoScan = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
    }
}

public sealed class SaudeDiscoViewModel
{
    public SaudeDiscoViewModel(SaudeDisco d)
    {
        Modelo = d.Modelo.Length > 45 ? d.Modelo[..42] + "…" : d.Modelo;

        HorasUso = d.HorasUso > 0
            ? $"{d.HorasUso:N0} h de uso  ({d.HorasUso / 8760.0:F1} anos)"
            : "Tempo de uso não disponível";

        TbwTexto = d.TbwFabricanteGb > 0
            ? $"{d.TbwEscritoGb:N0} GB escritos  /  {d.TbwFabricanteGb:N0} GB máx (fabricante)"
            : d.TbwEscritoGb > 0
                ? $"{d.TbwEscritoGb:N0} GB escritos  /  máx. desconhecido"
                : "TBW não disponível neste disco";

        Porcentagem = d.PorcentagemVidaRestante;
        PorcentagemTexto = $"{d.PorcentagemVidaRestante:F0}%";

        (NivelTexto, CorNivel, CorFundo) = d.Nivel switch
        {
            NivelSaudeDisco.Bom      => ("BOM",     new SolidColorBrush(Color.Parse("#00FF88")), new SolidColorBrush(Color.Parse("#00FF8820"))),
            NivelSaudeDisco.Atencao  => ("ATENÇÃO", new SolidColorBrush(Color.Parse("#FFCC00")), new SolidColorBrush(Color.Parse("#FFCC0020"))),
            _                        => ("CRÍTICO", new SolidColorBrush(Color.Parse("#FF3333")), new SolidColorBrush(Color.Parse("#FF333320"))),
        };

        TemErros = d.TemErrosNaoCorrigiveis || d.SetoresComProblema > 0;
        AvisoErro = d.TemErrosNaoCorrigiveis
            ? "⚠  Erros não corrigíveis detectados — faça backup imediatamente"
            : d.SetoresComProblema > 0
                ? $"⚠  {d.SetoresComProblema} setor(es) com problema detectado(s)"
                : string.Empty;
    }

    public string Modelo           { get; }
    public string HorasUso         { get; }
    public string TbwTexto         { get; }
    public double Porcentagem      { get; }
    public string PorcentagemTexto { get; }
    public string NivelTexto       { get; }
    public IBrush CorNivel         { get; }
    public IBrush CorFundo         { get; }
    public bool   TemErros         { get; }
    public string AvisoErro        { get; }
}
