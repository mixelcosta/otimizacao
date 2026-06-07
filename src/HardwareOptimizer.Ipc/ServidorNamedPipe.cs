using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Ipc;

/// <summary>
/// Servidor IPC sobre named pipe. Atende uma conexão por vez (suficiente para
/// uma UI local): lê uma requisição JSON por linha, roteia e responde. Usa modo
/// Byte para funcionar também em Linux/macOS.
/// </summary>
public sealed class ServidorNamedPipe
{
    private readonly string _nomePipe;
    private readonly RoteadorIpc _roteador;
    private readonly ILogger _log;

    public ServidorNamedPipe(string nomePipe, RoteadorIpc? roteador = null, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomePipe);
        _nomePipe = nomePipe;
        _roteador = roteador ?? new RoteadorIpc();
        _log = logger ?? NullLogger.Instance;
    }

    public async Task ServirAsync(CancellationToken cancellationToken = default)
    {
        _log.LogInformation("IPC: servidor escutando no pipe '{Pipe}'.", _nomePipe);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var servidor = new NamedPipeServerStream(
                _nomePipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await servidor.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await AtenderAsync(servidor, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AtenderAsync(Stream fluxo, CancellationToken cancellationToken)
    {
        using var leitor = new StreamReader(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
        await using var escritor = new StreamWriter(fluxo, new UTF8Encoding(false)) { AutoFlush = true };

        var linha = await leitor.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(linha))
        {
            return;
        }

        RespostaIpc resposta;
        try
        {
            var requisicao = JsonSerializer.Deserialize<RequisicaoIpc>(linha, ProtocoloIpc.Json);
            resposta = requisicao is null
                ? RespostaIpc.Falha("?", "Requisição vazia.")
                : await _roteador.TratarAsync(requisicao, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            resposta = RespostaIpc.Falha("?", "Requisição inválida: " + ex.Message);
        }

        await escritor.WriteLineAsync(JsonSerializer.Serialize(resposta, ProtocoloIpc.Json)).ConfigureAwait(false);
    }
}
