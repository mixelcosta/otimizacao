namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Cliente multimodal: recebe uma imagem + prompts e devolve texto/JSON.
/// Abstrai o provedor (SDK Anthropic) e permite testar com um cliente falso.
/// </summary>
public interface IClienteVisao
{
    string Modelo { get; }

    Task<string> AnalisarAsync(
        ImagemEntrada imagem,
        string promptSistema,
        string promptUsuario,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validação/pré-processamento da imagem antes do envio. Mantém o passo do
/// pipeline; o redimensionamento real (via biblioteca de imagem) é um próximo
/// passo, mas o tipo e o tamanho já são checados aqui.
/// </summary>
public sealed class PreProcessadorImagem
{
    /// <summary>Limite de aviso de tamanho (~4 MB de base64).</summary>
    private const int LimiteBase64 = 4 * 1024 * 1024;

    private static readonly HashSet<string> Suportados =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp", "image/gif" };

    public IReadOnlyList<string> Validar(ImagemEntrada imagem)
    {
        ArgumentNullException.ThrowIfNull(imagem);

        if (string.IsNullOrWhiteSpace(imagem.Base64))
        {
            throw new ArgumentException("Imagem vazia.", nameof(imagem));
        }

        if (!Suportados.Contains(imagem.MediaType))
        {
            throw new NotSupportedException($"Tipo de imagem não suportado: {imagem.MediaType}");
        }

        var avisos = new List<string>();
        if (imagem.Base64.Length > LimiteBase64)
        {
            avisos.Add("Imagem grande; considere redimensionar antes do envio para reduzir custo/tempo.");
        }

        return avisos;
    }
}
