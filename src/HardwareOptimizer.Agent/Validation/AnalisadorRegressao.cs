using System.Globalization;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Decide se houve regressão a partir das métricas medidas (e de um baseline
/// opcional para a comparação antes/depois). Sinais inequívocos — WHEA, erros
/// de memória, artefatos, TDR, BSOD, superaquecimento — reprovam a categoria.
/// Produz o contrato <see cref="ResultadoValidacao"/>.
/// </summary>
public sealed class AnalisadorRegressao
{
    public ResultadoValidacao Analisar(
        CategoriaAcao categoria,
        string ferramenta,
        MedicaoEstresse atual,
        MedicaoEstresse? baseline,
        LimiaresValidacao limiares)
    {
        ArgumentNullException.ThrowIfNull(atual);
        ArgumentNullException.ThrowIfNull(limiares);

        var erros = new List<string>();

        if (atual.ErrosWhea > 0)
        {
            erros.Add($"WHEA: {atual.ErrosWhea}");
        }

        if (atual.ErrosMemoria > 0)
        {
            erros.Add($"Erros de memória: {atual.ErrosMemoria}");
        }

        if (atual.Artefatos)
        {
            erros.Add("Artefatos gráficos detectados");
        }

        if (atual.DriverTimeout)
        {
            erros.Add("Driver timeout (TDR)");
        }

        if (atual.TelaAzul)
        {
            erros.Add("Tela azul (BSOD)");
        }

        if (atual.TempMaxC is { } temp && temp > limiares.TempMaxAceitavelC)
        {
            erros.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Temperatura {temp}°C acima do limite {limiares.TempMaxAceitavelC}°C"));
        }

        if (baseline?.Pontuacao is { } baseScore && atual.Pontuacao is { } score
            && score < baseScore * (1 - limiares.MargemQuedaPontuacao))
        {
            erros.Add(string.Create(
                CultureInfo.InvariantCulture, $"Queda de pontuação: {score} < {baseScore}"));
        }

        var regressao = erros.Count > 0;
        return new ResultadoValidacao
        {
            Categoria = categoria.ToString(),
            Ferramenta = ferramenta,
            Antes = baseline is null ? null : ParaMedicaoTeste(baseline),
            Depois = ParaMedicaoTeste(atual),
            Regressao = regressao,
            Erros = erros,
            Estabilidade = regressao ? "Reprovado" : "Totalmente validado",
        };
    }

    private static MedicaoTeste ParaMedicaoTeste(MedicaoEstresse m) => new()
    {
        Score = m.Pontuacao,
        TempMaxC = m.TempMaxC,
        ClockMhz = m.ClockMhz,
        ConsumoW = m.ConsumoW,
    };
}
