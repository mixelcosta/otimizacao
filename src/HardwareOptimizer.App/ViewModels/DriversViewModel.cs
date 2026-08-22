using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class DriversViewModel : ObservableObject
{
    private readonly IRoteadorIpc? _agente;
    private IReadOnlyList<InfoDriver> _todosDrivers = [];
    private IReadOnlyList<ProgramaInstalado> _programasInstalados = [];

    public DriversViewModel(IRoteadorIpc? agente = null)
    {
        _agente = agente;
        Drivers = new ObservableCollection<InfoDriverViewModel>();
        Software = new ObservableCollection<InfoSoftwareViewModel>();
    }

    [ObservableProperty] private string _statusText = "Execute o SCAN para listar os drivers instalados.";
    [ObservableProperty] private string _ultimoScan = string.Empty;
    [ObservableProperty] private bool _temResultados;
    [ObservableProperty] private string _filtroTexto = string.Empty;
    [ObservableProperty] private string _backupStatus = string.Empty;
    [ObservableProperty] private bool _exportando;
    [ObservableProperty] private bool _escaneando;
    [ObservableProperty] private bool _instalando;
    [ObservableProperty] private string _statusInstalacao = string.Empty;

    // ── Software desatualizado ───────────────────────────────────────────────
    [ObservableProperty] private bool _verificandoSoftware;
    [ObservableProperty] private string _statusTextSoftware = "Execute o SCAN para verificar software desatualizado.";
    [ObservableProperty] private bool _temResultadosSoftware;

    // ── Estado do Confirmation Panel ────────────────────────────────────────
    [ObservableProperty] private InfoDriverViewModel? _driverSelecionado;
    [ObservableProperty] private bool _painelConfirmacaoAberto;
    [ObservableProperty] private bool _confirmado;
    [ObservableProperty] private string _mensagemConfirmacao = string.Empty;
    [ObservableProperty] private string? _caminhoBackupAtual;

    /// <summary>
    /// Ganho/custo estimados do item selecionado. Contratos existem a partir
    /// desta história (Core/Contracts) — o primeiro consumo real com dados
    /// calculados é a Story 3.4/3.5 (ganho) e 3.8 (custo); aqui permanecem
    /// disponíveis, porém não populados, para o fluxo de driver.
    /// </summary>
    [ObservableProperty] private GanhoEstimado? _ganhoEstimadoAtual;
    [ObservableProperty] private Custo? _custoAtual;

    public bool PodeExportarBackup => _agente is not null;

    partial void OnFiltroTextoChanged(string value) => AplicarFiltro();

    public ObservableCollection<InfoDriverViewModel> Drivers { get; }

    public ObservableCollection<InfoSoftwareViewModel> Software { get; }

    public void Popular(IReadOnlyList<InfoDriver> drivers)
    {
        _todosDrivers = drivers.OrderBy(x => x.Descricao).ToList();
        AplicarFiltro();
        UltimoScan = $"Último scan: {DateTime.Now:HH:mm  dd/MM/yyyy}";
    }

    /// <summary>
    /// Guarda a lista de programas instalados coletada no SCAN inicial (mesmo
    /// <c>Inventario.ProgramasInstalados</c> já usado por
    /// <c>OtimizadorWindowsViewModel</c>) para uso posterior por
    /// <see cref="VerificarSoftwareAsync"/> — nenhum coletor novo é criado
    /// (Boundaries §Never).
    /// </summary>
    public void PopularProgramas(IReadOnlyList<ProgramaInstalado> programas)
    {
        _programasInstalados = programas;
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

    /// <summary>
    /// Varredura via <c>varrerdrivers</c> (IProvedorFonteOficial por trás do
    /// IPC) — versão atual vs. oficial mais recente, com fallback para
    /// "Desconhecido" quando a fonte falha (I/O Matrix).
    /// </summary>
    [RelayCommand]
    private async Task EscanearAsync()
    {
        if (_agente is null || Escaneando) return;

        Escaneando = true;
        StatusText = "Escaneando drivers…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "varrerdrivers" });
            if (resp.Sucesso && resp.Resultado is IReadOnlyList<InfoDriver> drivers)
            {
                Popular(drivers);
            }
            else
            {
                StatusText = $"Falha ao escanear: {resp.Erro}";
            }
        }
        finally
        {
            Escaneando = false;
        }
    }

    /// <summary>Abre o Confirmation Panel inline para o driver selecionado.</summary>
    [RelayCommand]
    private void AbrirConfirmacao(InfoDriverViewModel? driver)
    {
        if (driver is null) return;

        DriverSelecionado = driver;
        PainelConfirmacaoAberto = true;
        Confirmado = false;
        CaminhoBackupAtual = null;
        GanhoEstimadoAtual = null;
        CustoAtual = null;
        StatusInstalacao = string.Empty;
        MensagemConfirmacao =
            $"Atualizar \"{driver.Descricao}\" para a versão mais recente conhecida. " +
            "Um backup dos drivers atuais será criado antes da instalação — " +
            "sem backup bem-sucedido, a instalação não prossegue.";
    }

    [RelayCommand]
    private void FecharConfirmacao()
    {
        PainelConfirmacaoAberto = false;
        DriverSelecionado = null;
        Confirmado = false;
    }

    /// <summary>
    /// Fluxo de aprovação: backup obrigatório antes da instalação, via
    /// <c>aprovaratualizacaodriver</c>. Nunca dispara sem o usuário ter
    /// confirmado explicitamente no painel (botão de aplicar desabilitado até
    /// então — gate vive no próprio <c>ConfirmationPanel</c>).
    /// </summary>
    [RelayCommand]
    private async Task AplicarAtualizacaoAsync()
    {
        var driver = DriverSelecionado;
        if (driver?.UrlDownload is null || _agente is null || Instalando || !Confirmado) return;

        Instalando = true;
        StatusInstalacao = "Fazendo backup dos drivers atuais…";
        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                urlDownload = driver.UrlDownload,
                descricao = driver.Descricao,
            });
            var resp = await _agente.TratarAsync(
                new RequisicaoIpc { Metodo = "aprovaratualizacaodriver", Parametros = payload });

            if (resp.Sucesso && resp.Resultado is ResultadoAprovacaoDriverDto dto)
            {
                CaminhoBackupAtual = dto.CaminhoBackup;
                StatusInstalacao = dto.Sucesso
                    ? $"✓ Driver instalado. Backup em: {dto.CaminhoBackup}"
                    : $"Falha: {dto.Erro}" + (dto.CaminhoBackup is null ? "" : $" (backup disponível em: {dto.CaminhoBackup})");
            }
            else
            {
                StatusInstalacao = $"Falha: {resp.Erro}";
            }
        }
        finally
        {
            Instalando = false;
            // Cada tentativa exige nova confirmação explícita — sucesso ou falha,
            // o gate do painel volta a ficar fechado até o usuário reconfirmar.
            Confirmado = false;
        }
    }

    /// <summary>
    /// Rollback acionado pelo usuário via <c>reverteratualizacaodriver</c> —
    /// nunca automático. Requer um backup já exportado nesta sessão do painel.
    /// </summary>
    [RelayCommand]
    private async Task ReverterAsync()
    {
        if (_agente is null || string.IsNullOrEmpty(CaminhoBackupAtual) || Instalando) return;

        Instalando = true;
        StatusInstalacao = "Revertendo a partir do backup…";
        try
        {
            var payload = JsonSerializer.SerializeToElement(new { caminhoBackup = CaminhoBackupAtual });
            var resp = await _agente.TratarAsync(
                new RequisicaoIpc { Metodo = "reverteratualizacaodriver", Parametros = payload });

            StatusInstalacao = resp.Sucesso
                ? "✓ Rollback concluído a partir do backup."
                : $"Falha no rollback: {resp.Erro}";
        }
        finally
        {
            Instalando = false;
        }
    }

    [RelayCommand]
    private void AbrirDownload(InfoDriverViewModel? driver)
    {
        if (driver?.UrlDownload is null) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(driver.UrlDownload) { UseShellExecute = true });
    }

    /// <summary>
    /// Verifica software desatualizado via <c>verificarsoftware</c>
    /// (IProvedorFonteOficial por trás do IPC, mesma fronteira dos drivers) —
    /// itens sem cobertura no catálogo ou já atualizados nunca aparecem
    /// (guard anti-alucinação, I/O Matrix da spec-1-3).
    /// </summary>
    [RelayCommand]
    private async Task VerificarSoftwareAsync()
    {
        if (_agente is null || VerificandoSoftware) return;

        VerificandoSoftware = true;
        StatusTextSoftware = "Verificando software…";
        try
        {
            // Serializado com ProtocoloIpc.Json (mesmas opções usadas pelo lado
            // servidor em RoteadorIpc.VerificarSoftwareAsync) — sem isso, o
            // "Nome" (PascalCase, .NET default) de ProgramaInstalado não bate
            // com a leitura camelCase do lado servidor e a desserialização falha.
            var payload = JsonSerializer.SerializeToElement(
                new { programas = _programasInstalados }, ProtocoloIpc.Json);
            var resp = await _agente.TratarAsync(
                new RequisicaoIpc { Metodo = "verificarsoftware", Parametros = payload });

            if (resp.Sucesso && resp.Resultado is IReadOnlyList<InfoSoftware> lista)
            {
                Software.Clear();
                foreach (var s in lista.OrderBy(x => x.Nome))
                    Software.Add(new InfoSoftwareViewModel(s));

                TemResultadosSoftware = Software.Count > 0;
                StatusTextSoftware = Software.Count == 0
                    ? "Nenhum software desatualizado encontrado."
                    : $"{Software.Count} programa(s) com atualização disponível.";
            }
            else
            {
                Software.Clear();
                TemResultadosSoftware = false;
                StatusTextSoftware = $"Falha ao verificar software: {resp.Erro}";
            }
        }
        finally
        {
            VerificandoSoftware = false;
        }
    }

    /// <summary>
    /// Só abre a URL oficial no navegador padrão — mesmo padrão de
    /// <see cref="AbrirDownload"/>. Nenhum download/instalação pelo app
    /// (Boundaries §Always da spec-1-3).
    /// </summary>
    [RelayCommand]
    private void AbrirDownloadSoftware(InfoSoftwareViewModel? software)
    {
        if (string.IsNullOrEmpty(software?.UrlDownload)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(software.UrlDownload) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ExportarBackupAsync()
    {
        if (_agente is null || Exportando) return;

        Exportando = true;
        BackupStatus = "Exportando drivers via pnputil…";
        try
        {
            var resp = await _agente.TratarAsync(
                new RequisicaoIpc { Metodo = "exportarbackupdrivers" });

            BackupStatus = resp.Sucesso
                ? $"Backup salvo em: {resp.Resultado}"
                : "Falha: " + resp.Erro;
        }
        finally
        {
            Exportando = false;
        }
    }
}

public sealed class InfoDriverViewModel
{
    public InfoDriverViewModel(InfoDriver d)
    {
        HardwareId = d.HardwareId;
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

        UrlDownload = d.UrlDownload;
        TemDownload = d.Status == StatusDriver.AtualizacaoDisponivel
                      && !string.IsNullOrEmpty(d.UrlDownload);

        var hwid = d.HardwareId;
        HwidCurto = hwid.Length > 50 ? hwid[..47] + "…" : hwid;
    }

    public string  HardwareId     { get; }
    public string  Descricao      { get; }
    public string  Fabricante     { get; }
    public string  VersaoAtual    { get; }
    public string  StatusTexto    { get; }
    public IBrush  CorStatus      { get; }
    public IBrush  CorFundo       { get; }
    public bool    CertificadoWhql { get; }
    public string? UrlDownload    { get; }
    public bool    TemDownload    { get; }
    public string  HwidCurto     { get; }
}

public sealed class InfoSoftwareViewModel
{
    public InfoSoftwareViewModel(InfoSoftware s)
    {
        Nome = s.Nome;
        VersaoAtual = string.IsNullOrWhiteSpace(s.VersaoAtual) ? "—" : s.VersaoAtual;
        VersaoDisponivel = string.IsNullOrWhiteSpace(s.VersaoDisponivel) ? "—" : s.VersaoDisponivel;

        (StatusTexto, CorStatus, CorFundo) = s.Status switch
        {
            StatusSoftware.Atualizado            => ("ATUALIZADO",  new SolidColorBrush(Color.Parse("#00FF88")), new SolidColorBrush(Color.Parse("#00FF8815"))),
            StatusSoftware.AtualizacaoDisponivel => ("ATUALIZAÇÃO", new SolidColorBrush(Color.Parse("#FFCC00")), new SolidColorBrush(Color.Parse("#FFCC0015"))),
            _                                     => ("—",           new SolidColorBrush(Color.Parse("#484865")), new SolidColorBrush(Color.Parse("#48486510"))),
        };

        UrlDownload = s.UrlDownload;
        TemDownload = !string.IsNullOrEmpty(s.UrlDownload);
    }

    public string  Nome             { get; }
    public string  VersaoAtual      { get; }
    public string  VersaoDisponivel { get; }
    public string  StatusTexto      { get; }
    public IBrush  CorStatus        { get; }
    public IBrush  CorFundo         { get; }
    public string? UrlDownload      { get; }
    public bool    TemDownload      { get; }
}
