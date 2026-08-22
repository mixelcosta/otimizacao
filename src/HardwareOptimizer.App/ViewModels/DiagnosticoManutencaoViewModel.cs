using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

/// <summary>
/// Diagnóstico de manutenção (spec-2-1): dispara <c>diagnosticarmanutencao</c>
/// sob demanda (botão) — nunca automático — e exibe o achado só quando o
/// backend sinaliza suspeita real de pasta térmica ressecada (guard
/// anti-alucinação já aplicado em <c>DetectorPastaTermica</c>; esta tela apenas
/// reflete o resultado, nenhuma pergunta de diagnóstico é feita ao usuário).
/// </summary>
public partial class DiagnosticoManutencaoViewModel : ObservableObject
{
    private readonly IRoteadorIpc? _agente;

    public DiagnosticoManutencaoViewModel(IRoteadorIpc? agente = null)
    {
        _agente = agente;
    }

    [ObservableProperty] private bool _diagnosticando;
    [ObservableProperty] private bool _jaDiagnosticou;
    [ObservableProperty] private AchadoManutencao? _achado;
    [ObservableProperty] private string _statusText =
        "Compara a temperatura do seu hardware em repouso com uma carga simulada de alguns segundos, " +
        "gerada internamente pelo app — sem precisar de nenhuma ferramenta externa. " +
        "Durante o diagnóstico, todos os núcleos da CPU ficam ocupados por alguns segundos.";

    public bool TemAchado => Achado is not null;

    /// <summary>
    /// Texto de exibição da temperatura sob carga — "—" quando o backend não
    /// tem uma leitura real (<c>AchadoManutencao.TemperaturaCargaC</c> nulo,
    /// corrigido na revisão independente: nunca mostrar um valor fabricado
    /// como se fosse uma medição sob carga real).
    /// </summary>
    public string TemperaturaCargaTexto =>
        Achado?.TemperaturaCargaC is { } c
            ? c.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " °C"
            : "—";

    partial void OnAchadoChanged(AchadoManutencao? value)
    {
        OnPropertyChanged(nameof(TemAchado));
        OnPropertyChanged(nameof(TemperaturaCargaTexto));
    }

    /// <summary>
    /// Dispara o fluxo real via <c>diagnosticarmanutencao</c> — leitura idle,
    /// carga simulada interna e leitura sob carga, tudo no agente. Sempre sob
    /// demanda (Boundaries §Always da spec-2-1: leitura de sensor nunca em
    /// timer/daemon/background).
    /// </summary>
    [RelayCommand]
    private async Task DiagnosticarAsync()
    {
        if (_agente is null || Diagnosticando) return;

        Diagnosticando = true;
        Achado = null;
        StatusText = "Lendo temperatura em repouso, gerando carga simulada e lendo novamente…";
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "diagnosticarmanutencao" });
            JaDiagnosticou = true;

            if (resp.Sucesso)
            {
                Achado = resp.Resultado as AchadoManutencao;
                StatusText = Achado is not null
                    ? "Possível pasta térmica ressecada ou necessidade de limpeza detectada."
                    : "Nenhum sinal de pasta térmica ressecada detectado — temperatura em repouso dentro do esperado.";
            }
            else
            {
                StatusText = $"Falha ao diagnosticar: {resp.Erro}";
            }
        }
        finally
        {
            Diagnosticando = false;
        }
    }
}
