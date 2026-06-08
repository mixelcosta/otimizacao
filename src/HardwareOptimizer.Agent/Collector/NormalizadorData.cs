using System.Globalization;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Normaliza datas vindas das fontes de inventário para o formato ISO
/// <c>yyyy-MM-dd</c>. Cobre o formato legado do <c>ConvertTo-Json</c> do Windows
/// PowerShell (<c>/Date(ms)/</c>), o CIM DATETIME bruto (<c>yyyyMMddHHmmss…</c>) e
/// o formato do DMI no Linux (<c>MM/dd/yyyy</c>). Pura e determinística.
/// </summary>
internal static class NormalizadorData
{
    /// <summary>Devolve a data em ISO (yyyy-MM-dd) ou o texto original se não reconhecer; nulo se vazio.</summary>
    public static string? Normalizar(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return null;
        }

        var texto = bruto.Trim();

        if (TentarDateJson(texto, out var data)
            || TentarCimDatetime(texto, out data)
            || TentarFormatosComuns(texto, out data))
        {
            return data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return texto; // formato não reconhecido: devolve como veio (não perde informação)
    }

    // /Date(1754611200000)/  ou  /Date(1754611200000+0000)/
    private static bool TentarDateJson(string texto, out DateTimeOffset data)
    {
        data = default;
        var inicio = texto.IndexOf("/Date(", StringComparison.Ordinal);
        if (inicio < 0)
        {
            return false;
        }

        var fim = texto.IndexOf(")/", inicio, StringComparison.Ordinal);
        if (fim <= inicio)
        {
            return false;
        }

        var conteudo = texto[(inicio + 6)..fim];
        var sinal = conteudo.IndexOfAny(new[] { '+', '-' }, 1);
        var milissegundos = sinal > 0 ? conteudo[..sinal] : conteudo;

        if (long.TryParse(milissegundos, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
        {
            data = DateTimeOffset.FromUnixTimeMilliseconds(epoch);
            return true;
        }

        return false;
    }

    // CIM DATETIME: 20250808000000.000000+000  (usa os 8 primeiros dígitos)
    private static bool TentarCimDatetime(string texto, out DateTimeOffset data)
    {
        data = default;
        return texto.Length >= 8
            && texto[..8].All(char.IsDigit)
            && DateTimeOffset.TryParseExact(
                texto[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out data);
    }

    // MM/dd/yyyy (DMI/Linux) e variações ISO.
    private static bool TentarFormatosComuns(string texto, out DateTimeOffset data)
    {
        string[] formatos = { "MM/dd/yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:sszzz" };
        return DateTimeOffset.TryParseExact(
            texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out data);
    }
}
