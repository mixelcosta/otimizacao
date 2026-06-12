using System.IO.Pipes;
using System.Text.Json;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.WindowsService;

/// <summary>
/// Worker que lê sensores a cada 500ms, detecta anomalias e publica alertas
/// no named pipe "otimize-alertas" para o App consumir.
/// </summary>
public sealed class MonitorWorker : BackgroundService
{
    private const string PipeAlerts = "otimize-alertas";
    private const double LimiarRamPorcentagem = 85.0;
    private const double LimiarCpuSpike = 90.0;
    private const int JanelaSpikeSeg = 5;

    private readonly ServicoSensores _sensores;
    private readonly ILogger<MonitorWorker> _log;
    private readonly Queue<double> _historicoCpu = new(JanelaSpikeSeg * 2 + 1);

    public MonitorWorker(ILogger<MonitorWorker> log, ILoggerFactory loggerFactory)
    {
        _log = log;
        _sensores = new ServicoSensores(loggerFactory: loggerFactory);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Monitor de hardware iniciado.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var leitura = await _sensores.LerAsync(ct);
                VerificarAnomalias(leitura);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Erro na leitura de sensores.");
            }

            await Task.Delay(500, ct);
        }
    }

    private void VerificarAnomalias(LeituraSensores leitura)
    {
        var cpuUso = ExtrairValor(leitura, TipoSensor.Carga, "CPU Total");
        if (cpuUso.HasValue)
        {
            if (_historicoCpu.Count >= JanelaSpikeSeg * 2)
                _historicoCpu.Dequeue();
            _historicoCpu.Enqueue(cpuUso.Value);

            if (_historicoCpu.Count >= JanelaSpikeSeg &&
                _historicoCpu.All(v => v > LimiarCpuSpike))
            {
                PublicarAlerta($"CPU acima de {LimiarCpuSpike}% por {JanelaSpikeSeg}+ segundos consecutivos.");
                _historicoCpu.Clear();
            }
        }

        var ramUso = ExtrairValor(leitura, TipoSensor.Carga, "[RAM]");
        if (ramUso.HasValue && ramUso.Value > LimiarRamPorcentagem)
            PublicarAlerta($"RAM em uso elevado: {ramUso.Value:F0}%");
    }

    private void PublicarAlerta(string mensagem)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(".", PipeAlerts,
                    PipeDirection.Out, PipeOptions.Asynchronous);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await pipe.ConnectAsync(cts.Token);

                var payload = JsonSerializer.Serialize(new { mensagem });
                await using var writer = new StreamWriter(pipe);
                await writer.WriteLineAsync(payload);
                _log.LogDebug("Alerta publicado: {Mensagem}", mensagem);
            }
            catch
            {
                // App não está escutando; descarta silenciosamente.
            }
        });
    }

    private static double? ExtrairValor(LeituraSensores leitura, TipoSensor tipo, string fragmento) =>
        leitura.Sensores
            .Where(s => s.Tipo == tipo &&
                        s.Nome.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            .Select(s => (double?)s.Valor)
            .FirstOrDefault();
}
