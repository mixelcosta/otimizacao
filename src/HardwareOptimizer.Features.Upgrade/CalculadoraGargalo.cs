using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Upgrade;

public sealed class CalculadoraGargalo
{
    private static readonly Dictionary<string, int> _scoresCpuConhecidos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "i9-14900K", 2800 }, { "i7-14700K", 2400 }, { "i5-14600K", 1900 },
            { "i5-13400", 1500 },  { "i3-13100", 900 },
            { "Ryzen 9 7950X", 3000 }, { "Ryzen 9 7900X", 2500 }, { "Ryzen 7 7700X", 2000 },
            { "Ryzen 5 7600X", 1700 }, { "Ryzen 5 7600", 1600 },
            { "Ryzen 9 5900X", 2200 }, { "Ryzen 7 5700X", 1800 }, { "Ryzen 5 5600X", 1500 },
            { "Ryzen 5 5600", 1400 },  { "Ryzen 5 3600", 1100 },
        };

    private static readonly Dictionary<string, int> _scoresGpuConhecidos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "RTX 4090", 3000 }, { "RTX 4080", 2400 }, { "RTX 4070 Ti", 2100 },
            { "RTX 4070", 1800 }, { "RTX 4060 Ti", 1500 }, { "RTX 4060", 1300 },
            { "RTX 3080", 2000 }, { "RTX 3070", 1700 }, { "RTX 3060 Ti", 1500 },
            { "RX 7900 XTX", 2600 }, { "RX 7800 XT", 1900 }, { "RX 7700 XT", 1700 },
            { "RX 6700 XT", 1500 }, { "RX 6600 XT", 1200 },
        };

    public GargaloResult Calcular(Inventario inventario)
    {
        var scoreCpu = ObterScore(inventario.Cpu.Nome, _scoresCpuConhecidos);
        var scoreGpu = inventario.Gpu.Count > 0
            ? ObterScore(inventario.Gpu[0].Nome, _scoresGpuConhecidos)
            : 500;

        if (scoreCpu == 0 && scoreGpu == 0)
        {
            return new GargaloResult
            {
                ComponenteLimitante = "Desconhecido",
                PorcentagemGargalo = 0,
                GanhoEstimadoPercent = 0,
                Descricao = "Componentes não encontrados no banco de referência.",
            };
        }

        if (scoreCpu == 0) scoreCpu = scoreGpu;
        if (scoreGpu == 0) scoreGpu = scoreCpu;

        double ratio = (double)scoreCpu / scoreGpu;
        string limitante;
        double pctGargalo;
        double ganhoEstimado;

        if (ratio < 0.7)
        {
            // CPU muito fraca em relação à GPU
            limitante = "CPU";
            pctGargalo = Math.Round((1 - ratio) * 100, 1);
            ganhoEstimado = Math.Round((1.0 / ratio - 1) * 100, 1);
        }
        else if (ratio > 1.4)
        {
            // GPU muito fraca
            limitante = "GPU";
            pctGargalo = Math.Round((1 - 1.0 / ratio) * 100, 1);
            ganhoEstimado = Math.Round((ratio - 1) * 100, 1);
        }
        else
        {
            limitante = "Balanceado";
            pctGargalo = 0;
            ganhoEstimado = 0;
        }

        return new GargaloResult
        {
            ComponenteLimitante = limitante,
            PorcentagemGargalo = pctGargalo,
            GanhoEstimadoPercent = ganhoEstimado,
            Descricao = limitante == "Balanceado"
                ? "Setup equilibrado; upgrades em qualquer componente trazem ganhos proporcionais."
                : $"{limitante} é o gargalo principal. Upgrade estimado: +{ganhoEstimado:F0}% de performance.",
        };
    }

    private static int ObterScore(string nome, Dictionary<string, int> scores)
    {
        foreach (var (fragmento, score) in scores)
            if (nome.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
                return score;
        return 0;
    }
}
