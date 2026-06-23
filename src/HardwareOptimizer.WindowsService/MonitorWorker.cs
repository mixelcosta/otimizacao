using System.IO.Pipes;
using System.Text.Json;
using HardwareOptimizer.Agent.Sensors;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.WindowsService;

/// <summary>
/// Worker que lê sensores a cada 500ms, detecta anomalias e publica alertas
/// no named pipe "otimize-alertas" para o App consumir.
/// </summary>
public sealed class MonitorWorker : BackgroundService
{
    private const string PipeAlerts = "otimize-alertas";

    private readonly ServicoSensores _sensores;
    private readonly DetectorAnomalias _detector;
    private readonly ILogger<MonitorWorker> _log;

    public MonitorWorker(ILogger<MonitorWorker> log, ILoggerFactory loggerFactory)
    {
        _log = log;
        _sensores = new ServicoSensores(loggerFactory: loggerFactory);
        _detector = new DetectorAnomalias();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Monitor de hardware iniciado.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var leitura = await _sensores.LerAsync(ct);
                foreach (var mensagem in _detector.Detectar(leitura))
                    PublicarAlerta(mensagem);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Erro na leitura de sensores.");
            }

            await Task.Delay(500, ct);
        }
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
}
