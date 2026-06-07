using System.Globalization;

namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Comparação de versões de BIOS tolerante aos formatos reais do mercado:
/// numéricos puros ("2806" vs "3405"), com prefixo ("F10" vs "F12") e
/// pontuados ("P3.60" vs "P3.70"). Compara token a token, números por valor e
/// texto por ordem, evitando o erro clássico de comparar versões como strings.
/// </summary>
public static class VersaoBios
{
    /// <summary>Retorna &lt;0 se a &lt; b, 0 se iguais, &gt;0 se a &gt; b.</summary>
    public static int Comparar(string? a, string? b)
    {
        var tokensA = Tokenizar(a ?? string.Empty);
        var tokensB = Tokenizar(b ?? string.Empty);

        var total = Math.Max(tokensA.Count, tokensB.Count);
        for (var i = 0; i < total; i++)
        {
            if (i >= tokensA.Count)
            {
                return -1;
            }

            if (i >= tokensB.Count)
            {
                return 1;
            }

            var comparacao = CompararToken(tokensA[i], tokensB[i]);
            if (comparacao != 0)
            {
                return comparacao;
            }
        }

        return 0;
    }

    /// <summary>Verdadeiro se <paramref name="candidata"/> é mais nova que <paramref name="atual"/>.</summary>
    public static bool EhMaisRecente(string? atual, string? candidata) =>
        Comparar(candidata, atual) > 0;

    private static int CompararToken(string a, string b)
    {
        var numericoA = long.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var valorA);
        var numericoB = long.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var valorB);

        if (numericoA && numericoB)
        {
            return valorA.CompareTo(valorB);
        }

        // Um token numérico ordena antes de um token textual de mesma posição.
        if (numericoA != numericoB)
        {
            return numericoA ? -1 : 1;
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Tokenizar(string versao)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < versao.Length)
        {
            // Ignora separadores comuns.
            if (versao[i] is '.' or '-' or '_' or ' ')
            {
                i++;
                continue;
            }

            var ehDigito = char.IsDigit(versao[i]);
            var inicio = i;
            while (i < versao.Length && char.IsDigit(versao[i]) == ehDigito
                && versao[i] is not ('.' or '-' or '_' or ' '))
            {
                i++;
            }

            var token = versao[inicio..i];
            // Remove zeros à esquerda de tokens numéricos para a comparação por valor.
            tokens.Add(ehDigito ? token.TrimStart('0') is { Length: > 0 } t ? t : "0" : token);
        }

        return tokens;
    }
}
