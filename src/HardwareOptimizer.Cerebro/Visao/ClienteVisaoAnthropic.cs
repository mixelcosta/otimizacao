using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Implementação multimodal de <see cref="IClienteVisao"/> sobre o SDK oficial
/// da Anthropic: envia a imagem (base64) + os prompts e devolve o texto. Modelo
/// e chave vêm de configuração/ambiente — nada é fixado no código.
/// </summary>
public sealed class ClienteVisaoAnthropic : IClienteVisao
{
    private readonly AnthropicClient _client;
    private readonly int _maxTokens;

    public ClienteVisaoAnthropic(string modelo, string? apiKey = null, int maxTokens = 2000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        Modelo = modelo;
        _maxTokens = maxTokens;
        _client = apiKey is null
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
    }

    public string Modelo { get; }

    public async Task<string> AnalisarAsync(
        ImagemEntrada imagem, string promptSistema, string promptUsuario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagem);
        ArgumentNullException.ThrowIfNull(promptSistema);
        ArgumentNullException.ThrowIfNull(promptUsuario);
        cancellationToken.ThrowIfCancellationRequested();

        var parametros = new MessageCreateParams
        {
            Model = Modelo,
            MaxTokens = _maxTokens,
            System = promptSistema,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = imagem.Base64,
                                MediaType = imagem.MediaType,
                            },
                        },
                        new TextBlockParam { Text = promptUsuario },
                    },
                },
            ],
        };

        var resposta = await _client.Messages.Create(parametros).ConfigureAwait(false);

        var sb = new StringBuilder();
        foreach (var bloco in resposta.Content.Select(b => b.Value).OfType<TextBlock>())
        {
            sb.Append(bloco.Text);
        }

        return sb.ToString();
    }
}
