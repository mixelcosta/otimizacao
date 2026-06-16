using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Licensing;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IRoteadorIpc _agente;
    private readonly IServicoLicenca _licenca;
    private readonly DispatcherTimer _timerSensores;

    public ShellViewModel(IRoteadorIpc agente, IServicoLicenca licenca)
    {
        _agente = agente;
        _licenca = licenca;

        Dashboard = new DashboardViewModel(agente);
        OtimizadorWindows = new OtimizadorWindowsViewModel(agente);
        IaCopiloto = new IaCopilotoViewModel(agente);
        InfoSistema = new InfoSistemaViewModel();
        Upgrade = new UpgradeViewModel(agente);
        VidaUtil = new VidaUtilViewModel();
        Drivers = new DriversViewModel();
        BiosGuide = new BiosGuideViewModel();
        Configuracoes = new ConfiguracoesViewModel(licenca, OnLicencaAlterada);
        Home = new HomeViewModel(agente, () => PaginaAtual = Dashboard, inv =>
        {
            InfoSistema.Popular(inv);
            OtimizadorWindows.Popular(inv.EntradasStartup);
            OtimizadorWindows.PopularProgramas(inv.ProgramasInstalados);
            VidaUtil.Popular(inv.SaudeDiscos);
            Drivers.Popular(inv.Drivers);
            BiosGuide.Popular(inv);
            ScanConcluido = true;
        });

        _ehPremium = licenca.TipoAtual == TipoLicenca.Premium;

        _timerSensores = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _timerSensores.Tick += async (_, _) => await TickSensoresAsync();
        _timerSensores.Start();

        PaginaAtual = Home;
    }

    public HomeViewModel Home { get; }
    public DashboardViewModel Dashboard { get; }
    public OtimizadorWindowsViewModel OtimizadorWindows { get; }
    public IaCopilotoViewModel IaCopiloto { get; }
    public InfoSistemaViewModel InfoSistema { get; }
    public UpgradeViewModel Upgrade { get; }
    public VidaUtilViewModel VidaUtil { get; }
    public DriversViewModel Drivers { get; }
    public BiosGuideViewModel BiosGuide { get; }
    public ConfiguracoesViewModel Configuracoes { get; }

    [ObservableProperty] private ObservableObject _paginaAtual = null!;
    [ObservableProperty] private bool _temAlertaIa;
    [ObservableProperty] private bool _ehPremium;
    [ObservableProperty] private bool _scanConcluido;

    // Propriedades para binding de estado ativo na sidebar
    public bool PaginaEhDashboard    => PaginaAtual == Dashboard;
    public bool PaginaEhOtimizador   => PaginaAtual == OtimizadorWindows;
    public bool PaginaEhInfoSistema  => PaginaAtual == InfoSistema;
    public bool PaginaEhIaCopiloto   => PaginaAtual == IaCopiloto;
    public bool PaginaEhConfiguracoes => PaginaAtual == Configuracoes;
    public bool PaginaEhUpgrade      => PaginaAtual == Upgrade;
    public bool PaginaEhVidaUtil     => PaginaAtual == VidaUtil;
    public bool PaginaEhDrivers      => PaginaAtual == Drivers;
    public bool PaginaEhBiosGuide    => PaginaAtual == BiosGuide;

    partial void OnPaginaAtualChanged(ObservableObject value)
    {
        OnPropertyChanged(nameof(PaginaEhDashboard));
        OnPropertyChanged(nameof(PaginaEhOtimizador));
        OnPropertyChanged(nameof(PaginaEhInfoSistema));
        OnPropertyChanged(nameof(PaginaEhIaCopiloto));
        OnPropertyChanged(nameof(PaginaEhConfiguracoes));
        OnPropertyChanged(nameof(PaginaEhUpgrade));
        OnPropertyChanged(nameof(PaginaEhVidaUtil));
        OnPropertyChanged(nameof(PaginaEhDrivers));
        OnPropertyChanged(nameof(PaginaEhBiosGuide));
    }

    [RelayCommand] private void IrParaHome() => PaginaAtual = Home;

    [RelayCommand] private void IrParaDashboard() => PaginaAtual = Dashboard;

    [RelayCommand] private void IrParaOtimizador() => PaginaAtual = OtimizadorWindows;

    [RelayCommand]
    private void IrParaInfoSistema() => PaginaAtual = InfoSistema;

    [RelayCommand]
    private void IrParaIaCopiloto()
    {
        PaginaAtual = IaCopiloto;
        if (TemAlertaIa)
        {
            TemAlertaIa = false;
            IaCopiloto.MarcarComoLido();
        }
    }

    [RelayCommand]
    private void IrParaUpgrade()
    {
        if (!_licenca.TemAcesso(FuncionalidadePremium.ModuloUpgrade)) return;
        PaginaAtual = Upgrade;
        Dispatcher.UIThread.Post(async () => await Upgrade.AtivarAsync(),
            Avalonia.Threading.DispatcherPriority.Background);
    }

    [RelayCommand]
    private void IrParaVidaUtil()
    {
        if (!_licenca.TemAcesso(FuncionalidadePremium.ContadorVidaUtil)) return;
        PaginaAtual = VidaUtil;
    }

    [RelayCommand]
    private void IrParaDrivers()
    {
        if (!_licenca.TemAcesso(FuncionalidadePremium.GerenciadorDrivers)) return;
        PaginaAtual = Drivers;
    }

    [RelayCommand]
    private void IrParaBiosGuide()
    {
        if (!_licenca.TemAcesso(FuncionalidadePremium.GuiaBiosIa)) return;
        PaginaAtual = BiosGuide;
    }

    [RelayCommand]
    private void IrParaConfiguracoes() => PaginaAtual = Configuracoes;

    public void ReceberAlertaServico(string mensagem)
    {
        IaCopiloto.ReceberAlerta(mensagem);
        if (PaginaAtual != IaCopiloto)
            TemAlertaIa = true;
    }

    private void OnLicencaAlterada()
    {
        EhPremium = _licenca.TipoAtual == TipoLicenca.Premium;
    }

    private async Task TickSensoresAsync()
    {
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "sensores" });
            if (resp.Sucesso && resp.Resultado is LeituraSensores leitura)
                Dashboard.AtualizarSensores(leitura);
        }
        catch
        {
            // Sensor tick falha silenciosamente; a UI mostra "--" quando sem dados.
        }
    }

    public void Dispose() => _timerSensores.Stop();
}
