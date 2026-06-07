using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace HardwareOptimizer.Ipc;

/// <summary>
/// Cliente IPC sobre named pipe (usado pela UI). Abre conexão, envia uma
/// requisição e lê a resposta. No resultado, <see cref="RespostaIpc.Resultado"/>
/// chega como <see cref="JsonElement"/> para a camada de apresentação ler.
/// </summary>
public sealed class ClienteNamedPipe
{
    private readonly string _nomePipe;
    private readonly string _servidor;

    public ClienteNamedPipe(string nomePipe, string servidor = ".")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomePipe);
        _nomePipe = nomePipe;
        _servidor = servidor;
    }

    public async Task<RespostaIpc> ChamarAsync(
        RequisicaoIpc requisicao, CancellationToken cancellationToken = default, int timeoutMs = 5000)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        using var cliente = new NamedPipeClientStream(
            _servidor, _nomePipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await cliente.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);

        using var leitor = new StreamReader(cliente, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
        await using var escritor = new StreamWriter(cliente, new UTF8Encoding(false)) { AutoFlush = true };

        await escritor.WriteLineAsync(JsonSerializer.Serialize(requisicao, ProtocoloIpc.Json)).ConfigureAwait(false);

        var linha = await leitor.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return linha is null
            ? RespostaIpc.Falha(requisicao.Id, "Sem resposta do servidor.")
            : JsonSerializer.Deserialize<RespostaIpc>(linha, ProtocoloIpc.Json)
                ?? RespostaIpc.Falha(requisicao.Id, "Resposta vazia.");
    }

    /// <summary>Atalho para chamar um método sem parâmetros.</summary>
    public Task<RespostaIpc> ChamarAsync(string metodo, CancellationToken cancellationToken = default) =>
        ChamarAsync(new RequisicaoIpc { Metodo = metodo }, cancellationToken);
}
