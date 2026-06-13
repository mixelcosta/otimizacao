using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Implementação de <see cref="IClienteLlm"/> sobre o SDK oficial da Anthropic.
/// O modelo e a chave de API vêm de configuração/ambiente — nada é fixado no
/// código. Usa pensamento adaptativo, recomendado para tarefas de raciocínio.
/// </summary>
public sealed class ClienteLlmAnthropic : IClienteLlm
{
    private readonly AnthropicClient _client;
    private readonly int _maxTokens;

    /// <param name="modelo">ID do modelo Claude a usar (ex.: vindo de variável de ambiente).</param>
    /// <param name="apiKey">Chave de API. Se nula, o SDK lê de ANTHROPIC_API_KEY.</param>
    public ClienteLlmAnthropic(string modelo, string? apiKey = null, int maxTokens = 8000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        Modelo = modelo;
        _maxTokens = maxTokens;
        _client = apiKey is null
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
    }

    public string Modelo { get; }

    public async Task<string> ResponderAsync(
        string promptSistema, string promptUsuario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptSistema);
        ArgumentNullException.ThrowIfNull(promptUsuario);
        cancellationToken.ThrowIfCancellationRequested();

        var parametros = new MessageCreateParams
        {
            Model = Modelo,
            MaxTokens = _maxTokens,
            System = promptSistema,
            Thinking = new ThinkingConfigAdaptive(),
            Messages = [new() { Role = Role.User, Content = promptUsuario }],
        };

        var resposta = await _client.Messages.Create(parametros).ConfigureAwait(false);
        return ExtrairTexto(resposta);
    }

    public async Task<string> ResponderConversaAsync(
        string promptSistema,
        IReadOnlyList<(string Role, string Conteudo)> historico,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptSistema);
        ArgumentNullException.ThrowIfNull(historico);
        cancellationToken.ThrowIfCancellationRequested();

        var mensagens = historico
            .Select(m => new MessageParam
            {
                Role = m.Role == "assistant" ? Role.Assistant : Role.User,
                Content = m.Conteudo,
            })
            .ToList();

        var parametros = new MessageCreateParams
        {
            Model = Modelo,
            MaxTokens = _maxTokens,
            System = promptSistema,
            Messages = mensagens,
        };

        var resposta = await _client.Messages.Create(parametros).ConfigureAwait(false);
        return ExtrairTexto(resposta);
    }

    private static string ExtrairTexto(Message resposta)
    {
        var sb = new StringBuilder();
        foreach (var bloco in resposta.Content.Select(b => b.Value).OfType<TextBlock>())
            sb.Append(bloco.Text);
        return sb.ToString();
    }
}
