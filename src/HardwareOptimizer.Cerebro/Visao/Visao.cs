namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>Tipo de tela/imagem identificada (entradas do fluxo_visao).</summary>
public enum TipoTela
{
    Desconhecida = 0,
    BiosUefi = 1,
    EtiquetaPlaca = 2,
    MensagemErro = 3,
    Benchmark = 4,
}

/// <summary>Nível de confiança da leitura visual.</summary>
public enum NivelConfianca
{
    Baixa = 0,
    Media = 1,
    Alta = 2,
}

/// <summary>Caso de uso que direciona o prompt enviado ao modelo multimodal.</summary>
public enum CasoUsoVisao
{
    Identificar = 0,
    LerVersaoBios = 1,
    LerEtiquetaPlaca = 2,
    LerMensagemErro = 3,
    LerBenchmark = 4,
}

/// <summary>Desfecho do cruzamento da leitura visual com o inventário coletado.</summary>
public enum SituacaoConferencia
{
    Confere = 0,
    Diverge = 1,
    Inconclusivo = 2,
}

/// <summary>Imagem de entrada já em base64, pronta para o modelo multimodal.</summary>
public sealed record ImagemEntrada
{
    public required string Base64 { get; init; }

    /// <summary>image/png, image/jpeg, image/webp ou image/gif.</summary>
    public required string MediaType { get; init; }

    public string? Descricao { get; init; }

    public static ImagemEntrada DeArquivo(string caminho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        var bytes = File.ReadAllBytes(caminho);
        return new ImagemEntrada
        {
            Base64 = Convert.ToBase64String(bytes),
            MediaType = InferirMediaType(caminho),
            Descricao = Path.GetFileName(caminho),
        };
    }

    private static string InferirMediaType(string caminho) =>
        Path.GetExtension(caminho).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png",
        };
}

/// <summary>Leitura estruturada de uma imagem: tipo de tela, campos lidos e confiança.</summary>
public sealed record LeituraVisual
{
    public required TipoTela TipoTela { get; init; }

    public IReadOnlyDictionary<string, string> Campos { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public required NivelConfianca Confianca { get; init; }

    public string? ProximoPasso { get; init; }

    public string? TextoBruto { get; init; }

    public string? Modelo { get; init; }

    public string? Campo(string nome) => Campos.TryGetValue(nome, out var v) ? v : null;
}

/// <summary>Resultado do cruzamento da leitura visual com o inventário.</summary>
public sealed record ResultadoConferencia
{
    public required SituacaoConferencia Situacao { get; init; }

    public required string Mensagem { get; init; }

    /// <summary>Verdadeiro quando a confiança é baixa ou a leitura é inconclusiva.</summary>
    public bool PedirNovaFoto { get; init; }
}
