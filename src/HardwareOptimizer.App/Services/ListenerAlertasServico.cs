using System.IO.Pipes;
using System.Text.Json;

namespace HardwareOptimizer.App.Services;

public sealed class ListenerAlertasServico : IDisposable
{
    private const string PipeAlertas = "otimize-alertas";
    private readonly Action<string> _callback;
    private readonly CancellationTokenSource _cts = new();

    public ListenerAlertasServico(Action<string> callback)
    {
        _callback = callback;
        Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeAlertas, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(pipe);
                var linha = await reader.ReadLineAsync(ct);
                if (linha is not null)
                {
                    var payload = JsonSerializer.Deserialize<AlertaPayload>(linha,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (payload?.Mensagem is not null)
                        _callback(payload.Mensagem);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* App descarta se o pipe falhar ou o serviço não estiver rodando. */ }
        }
    }

    public void Dispose() => _cts.Cancel();

    private sealed record AlertaPayload(string? Mensagem);
}
