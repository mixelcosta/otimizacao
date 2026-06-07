using System.Text.Json;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Converte a resposta do modelo multimodal em uma <see cref="LeituraVisual"/>.
/// Defensivo: JSON ausente/ inválido vira leitura "desconhecida" com confiança
/// baixa e pedido de nova foto — nunca lança nem confia cegamente.
/// </summary>
public sealed class LeitorRespostaVisao
{
    public LeituraVisual Ler(string respostaModelo, string? modelo)
    {
        var json = ExtrairJson(respostaModelo);
        if (json is null)
        {
            return Indefinida(modelo, respostaModelo);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            return new LeituraVisual
            {
                TipoTela = MapearTipo(Texto(raiz, "tipoTela")),
                Campos = LerCampos(raiz),
                Confianca = MapearConfianca(Texto(raiz, "confianca")),
                ProximoPasso = Texto(raiz, "proximoPasso"),
                TextoBruto = respostaModelo,
                Modelo = modelo,
            };
        }
        catch (JsonException)
        {
            return Indefinida(modelo, respostaModelo);
        }
    }

    private static IReadOnlyDictionary<string, string> LerCampos(JsonElement raiz)
    {
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raiz.TryGetProperty("campos", out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                var valor = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };

                if (!string.IsNullOrWhiteSpace(valor))
                {
                    campos[prop.Name] = valor;
                }
            }
        }

        return campos;
    }

    private static TipoTela MapearTipo(string? valor) => Normalizar(valor) switch
    {
        "biosuefi" or "bios" or "uefi" => TipoTela.BiosUefi,
        "etiquetaplaca" or "etiqueta" => TipoTela.EtiquetaPlaca,
        "mensagemerro" or "erro" or "telaazul" => TipoTela.MensagemErro,
        "benchmark" or "estresse" => TipoTela.Benchmark,
        _ => TipoTela.Desconhecida,
    };

    private static NivelConfianca MapearConfianca(string? valor) => Normalizar(valor) switch
    {
        "alta" => NivelConfianca.Alta,
        "media" => NivelConfianca.Media,
        _ => NivelConfianca.Baixa,
    };

    private static string Normalizar(string? valor) =>
        new string((valor ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? Texto(JsonElement raiz, string propriedade) =>
        raiz.TryGetProperty(propriedade, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static LeituraVisual Indefinida(string? modelo, string? bruto) => new()
    {
        TipoTela = TipoTela.Desconhecida,
        Confianca = NivelConfianca.Baixa,
        ProximoPasso = "Não foi possível interpretar a imagem; envie outra foto, mais nítida.",
        TextoBruto = bruto,
        Modelo = modelo,
    };

    private static string? ExtrairJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{', StringComparison.Ordinal);
        var fim = texto.LastIndexOf('}');
        return inicio >= 0 && fim > inicio ? texto[inicio..(fim + 1)] : null;
    }
}
