using System.Globalization;
using System.Text.RegularExpressions;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Parser tolerante da saída de ferramentas de estresse. Lê linhas no formato
/// "chave: valor" e mapeia para <see cref="MedicaoEstresse"/>, normalizando a
/// chave e extraindo número/booleano do valor. Parsers específicos por
/// ferramenta podem especializar esta convenção.
/// </summary>
public sealed partial class ParserEstresse
{
    public MedicaoEstresse Parse(string saida)
    {
        ArgumentNullException.ThrowIfNull(saida);

        double? tempMax = null, clock = null, consumo = null, pontuacao = null;
        var whea = 0;
        var memoria = 0;
        bool artefatos = false, driverTimeout = false, telaAzul = false;

        foreach (var linha in saida.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = linha.IndexOf(':', StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var chave = Normalizar(linha[..idx]);
            var valor = linha[(idx + 1)..].Trim();

            switch (chave)
            {
                case "wheaerrors" or "whea":
                    whea = Inteiro(valor);
                    break;
                case "memoryerrors" or "memerrors" or "errosdememoria":
                    memoria = Inteiro(valor);
                    break;
                case "maxtemperature" or "maxtemp" or "temperatura" or "temperaturamaxima" or "temp":
                    tempMax = Numero(valor);
                    break;
                case "clock" or "frequencia":
                    clock = Numero(valor);
                    break;
                case "power" or "consumo":
                    consumo = Numero(valor);
                    break;
                case "score" or "pontuacao":
                    pontuacao = Numero(valor);
                    break;
                case "artifacts" or "artefatos":
                    artefatos = Booleano(valor);
                    break;
                case "drivertimeout" or "tdr":
                    driverTimeout = Booleano(valor);
                    break;
                case "bsod" or "telaazul" or "bluescreen":
                    telaAzul = Booleano(valor);
                    break;
                default:
                    break;
            }
        }

        return new MedicaoEstresse
        {
            TempMaxC = tempMax,
            ClockMhz = clock,
            ConsumoW = consumo,
            Pontuacao = pontuacao,
            ErrosWhea = whea,
            ErrosMemoria = memoria,
            Artefatos = artefatos,
            DriverTimeout = driverTimeout,
            TelaAzul = telaAzul,
        };
    }

    private static string Normalizar(string chave) =>
        new string(chave.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static double? Numero(string valor)
    {
        var m = NumeroRegex().Match(valor);
        return m.Success
            && double.TryParse(
                m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private static int Inteiro(string valor) => (int)(Numero(valor) ?? 0);

    private static bool Booleano(string valor)
    {
        var t = valor.Trim().ToLowerInvariant();
        return t is "yes" or "sim" or "true" or "1" or "detected" || (int.TryParse(t, out var n) && n > 0);
    }

    [GeneratedRegex(@"-?\d+(?:[.,]\d+)?")]
    private static partial Regex NumeroRegex();
}
