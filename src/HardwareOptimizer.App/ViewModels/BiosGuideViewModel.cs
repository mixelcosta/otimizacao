using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class BiosGuideViewModel : ObservableObject
{
    private readonly GeradorGuiaXmpExpo _gerador = new();
    private readonly IRoteadorIpc? _agente;

    public BiosGuideViewModel(IRoteadorIpc? agente = null)
    {
        _agente = agente;
        PassosXmp = new ObservableCollection<PassoGuiaViewModel>();
    }

    // ── Info da placa ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _fabricantePlaca = "–";
    [ObservableProperty] private string _modeloPlaca = "–";
    [ObservableProperty] private string _teclaSetup = "–";
    [ObservableProperty] private string _avisoXmp = string.Empty;
    [ObservableProperty] private int _passoAtual;

    // Badge de alerta: XMP não ativado detectado no scan
    [ObservableProperty] private bool   _alertaXmpAtivo;
    [ObservableProperty] private string _alertaXmpMensagem = "";

    public string PassoAtualInstrucao =>
        PassoAtual > 0 && PassoAtual <= PassosXmp.Count
            ? PassosXmp[PassoAtual - 1].Instrucao
            : string.Empty;

    public bool TemPassos => PassosXmp.Count > 0;

    public ObservableCollection<PassoGuiaViewModel> PassosXmp { get; }

    // ── Chat IA ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _pergunta = string.Empty;
    [ObservableProperty] private string _respostaIa = string.Empty;
    [ObservableProperty] private bool _perguntando;

    public bool TemResposta => !string.IsNullOrEmpty(RespostaIa);
    public bool ChatDisponivel => _agente is not null;

    partial void OnRespostaIaChanged(string value) => OnPropertyChanged(nameof(TemResposta));

    // ── Análise de foto ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _analisandoFoto;
    [ObservableProperty] private string _resultadoFoto = string.Empty;

    public bool TemResultadoFoto => !string.IsNullOrEmpty(ResultadoFoto);

    partial void OnResultadoFotoChanged(string value) => OnPropertyChanged(nameof(TemResultadoFoto));

    // ── Navegação stepper ─────────────────────────────────────────────────────
    public void Popular(Inventario inv)
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

        PassoAtual = PassosXmp.Count > 0 ? 1 : 0;
        OnPropertyChanged(nameof(PassoAtualInstrucao));
        OnPropertyChanged(nameof(TemPassos));

        // Detectar XMP desativado e gerar alerta
        if (inv.Memoria.Count > 0)
        {
            var m = inv.Memoria[0];
            if (m.VelocidadeMhz.HasValue && m.VelocidadeConfiguradaMhz.HasValue &&
                m.VelocidadeConfiguradaMhz.Value < m.VelocidadeMhz.Value * 0.9)
            {
                AlertaXmpAtivo = true;
                AlertaXmpMensagem = $"Sua RAM está a {m.VelocidadeConfiguradaMhz} MHz mas suporta {m.VelocidadeMhz} MHz. "
                    + "Ative XMP/EXPO na BIOS seguindo o guia abaixo para ganho de desempenho.";
            }
        }
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

    public async Task AnalisarFotoAsync(string caminhoArquivo)
    {
        if (string.IsNullOrEmpty(caminhoArquivo) || _agente is null || AnalisandoFoto) return;

        AnalisandoFoto = true;
        ResultadoFoto = string.Empty;

        try
        {
            var bytes = await File.ReadAllBytesAsync(caminhoArquivo).ConfigureAwait(false);
            var base64 = Convert.ToBase64String(bytes);
            var mediaType = Path.GetExtension(caminhoArquivo).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp"           => "image/webp",
                ".gif"            => "image/gif",
                _                 => "image/png",
            };

            var payload = JsonSerializer.SerializeToElement(new { imagemBase64 = base64, mediaType });
            var resp = await _agente.TratarAsync(
                new RequisicaoIpc { Metodo = "analisarbiosfoto", Parametros = payload });

            ResultadoFoto = resp.Sucesso && resp.Resultado is string s
                ? s
                : "Não foi possível interpretar a imagem. Tente uma foto mais nítida.";
        }
        catch (Exception ex)
        {
            ResultadoFoto = $"Erro ao processar imagem: {ex.Message}";
        }
        finally
        {
            AnalisandoFoto = false;
        }
    }

    [RelayCommand]
    private async Task PerguntarAsync()
    {
        if (string.IsNullOrWhiteSpace(Pergunta) || Perguntando || _agente is null) return;

        Perguntando = true;
        RespostaIa = string.Empty;

        try
        {
            var payload = JsonSerializer.SerializeToElement(new { pergunta = Pergunta.Trim() });
            var resp = await _agente.TratarAsync(
                new RequisicaoIpc { Metodo = "chat_bios", Parametros = payload });

            RespostaIa = resp.Sucesso && resp.Resultado is string s
                ? s
                : "Não foi possível obter resposta da IA.";
        }
        finally
        {
            Perguntando = false;
        }
    }
}

public sealed record PassoGuiaViewModel(int Numero, string Instrucao);
