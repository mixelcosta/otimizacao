using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class VidaUtilViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public VidaUtilViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        Discos = new ObservableCollection<SaudeDiscoViewModel>();
    }

    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _statusText = "Clique em \"Varrer Discos\" para ler os dados S.M.A.R.T.";
    [ObservableProperty] private string _ultimoScan = string.Empty;
    [ObservableProperty] private bool _temResultados;

    public ObservableCollection<SaudeDiscoViewModel> Discos { get; }

    [RelayCommand]
    private async Task VarrerAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        StatusText = "Lendo dados S.M.A.R.T. via WMI…";
        Discos.Clear();
        TemResultados = false;

        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "obtersaudediscos" });

            if (resp.Sucesso && resp.Resultado is IReadOnlyList<SaudeDisco> lista)
            {
                foreach (var d in lista)
                    Discos.Add(new SaudeDiscoViewModel(d));

                TemResultados = Discos.Count > 0;
                StatusText = Discos.Count > 0
                    ? $"{Discos.Count} disco(s) verificado(s)."
                    : "Nenhum disco S.M.A.R.T. encontrado neste sistema.";
            }
            else
            {
                StatusText = "S.M.A.R.T. não disponível ou sem permissão WMI.";
            }

            UltimoScan = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
        }
        finally { Ocupado = false; }
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

    public string Modelo          { get; }
    public string HorasUso        { get; }
    public string TbwTexto        { get; }
    public double Porcentagem     { get; }
    public string PorcentagemTexto { get; }
    public string NivelTexto      { get; }
    public IBrush CorNivel        { get; }
    public IBrush CorFundo        { get; }
    public bool   TemErros        { get; }
    public string AvisoErro       { get; }
}
