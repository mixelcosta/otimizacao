using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.WindowsService;

/// <summary>
/// Detecta anomalias de hardware em uma leitura de sensores.
/// Stateful: mantém o histórico de CPU para detectar spikes contínuos.
/// Extraído do MonitorWorker para permitir testes unitários sem hardware real.
/// </summary>
public sealed class DetectorAnomalias
{
    public const double LimiarRamPorcentagem = 85.0;
    public const double LimiarCpuSpike = 90.0;
    public const int JanelaSpikeSeg = 5;

    private readonly Queue<double> _historicoCpu = new(JanelaSpikeSeg * 2 + 1);

    /// <summary>
    /// Processa uma leitura e retorna as mensagens de alerta detectadas (pode ser vazio).
    /// </summary>
    public IReadOnlyList<string> Detectar(LeituraSensores leitura)
    {
        var alertas = new List<string>();

        var cpuUso = ExtrairValor(leitura, TipoSensor.Carga, "CPU Total");
        if (cpuUso.HasValue)
        {
            if (_historicoCpu.Count >= JanelaSpikeSeg * 2)
                _historicoCpu.Dequeue();
            _historicoCpu.Enqueue(cpuUso.Value);

            if (_historicoCpu.Count >= JanelaSpikeSeg &&
                _historicoCpu.All(v => v > LimiarCpuSpike))
            {
                alertas.Add($"CPU acima de {LimiarCpuSpike}% por {JanelaSpikeSeg}+ segundos consecutivos.");
                _historicoCpu.Clear();
            }
        }

        var ramUso = ExtrairValor(leitura, TipoSensor.Carga, "[RAM]");
        if (ramUso.HasValue && ramUso.Value > LimiarRamPorcentagem)
            alertas.Add($"RAM em uso elevado: {ramUso.Value:F0}%");

        return alertas;
    }

    public static double? ExtrairValor(LeituraSensores leitura, TipoSensor tipo, string fragmento) =>
        leitura.Sensores
            .Where(s => s.Tipo == tipo &&
                        s.Nome.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            .Select(s => (double?)s.Valor)
            .FirstOrDefault();
}
