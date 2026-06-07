using System.Globalization;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Reporting;

/// <summary>
/// Calcula as notas 0-100 por domínio a partir do inventário, dos resultados de
/// validação e dos domínios efetivamente otimizados. As heurísticas (v1) são
/// transparentes: cada critério contribui pontos explicáveis, somados e
/// limitados a [0, 100]. É lógica pura e determinística.
/// </summary>
public sealed class CalculadoraScore
{
    // Pesos dos domínios primários na nota final. Estabilidade pesa mais,
    // refletindo a ordem ESTABILIDADE > ... > DESEMPENHO da filosofia.
    private static readonly IReadOnlyDictionary<Dominio, double> Pesos = new Dictionary<Dominio, double>
    {
        [Dominio.Estabilidade] = 2.0,
        [Dominio.Cpu] = 1.5,
        [Dominio.Ram] = 1.2,
        [Dominio.Gpu] = 1.0,
        [Dominio.Bios] = 1.0,
        [Dominio.Windows] = 1.0,
    };

    public ResultadoScore Calcular(
        Inventario inventario,
        IReadOnlyList<ResultadoValidacao> validacoes,
        ISet<Dominio> dominiosOtimizados)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        ArgumentNullException.ThrowIfNull(validacoes);
        ArgumentNullException.ThrowIfNull(dominiosOtimizados);

        var bios = Bios(inventario);
        var cpu = Cpu(inventario);
        var gpu = Gpu(inventario);
        var ram = Ram(inventario);
        var windows = Windows(inventario, dominiosOtimizados);
        var estabilidade = Estabilidade(validacoes);
        var hardware = Hardware(cpu, gpu, ram, bios);

        var scores = new[] { hardware, bios, cpu, gpu, ram, windows, estabilidade };
        return new ResultadoScore { Scores = scores, NotaFinal = NotaFinal(scores) };
    }

    private static Score Bios(Inventario inv)
    {
        var criterios = new List<string>();
        var v = 0;

        switch (inv.Placa.Modo)
        {
            case "UEFI":
                v += 50;
                criterios.Add("Modo UEFI (+50)");
                break;
            case "Legacy":
                v += 20;
                criterios.Add("Modo Legacy (+20)");
                break;
            default:
                v += 25;
                criterios.Add("Modo de firmware desconhecido (+25)");
                break;
        }

        v += inv.Placa.SecureBoot switch
        {
            true => Pontuar(criterios, 30, "Secure Boot ativo"),
            false => Pontuar(criterios, 5, "Secure Boot inativo"),
            null => Pontuar(criterios, 15, "Secure Boot desconhecido"),
        };

        v += string.IsNullOrWhiteSpace(inv.Placa.VersaoBios)
            ? Pontuar(criterios, 0, "Versão de BIOS desconhecida")
            : Pontuar(criterios, 20, "Versão de BIOS identificada");

        return Montar(Dominio.Bios, v, criterios);
    }

    private static Score Cpu(Inventario inv)
    {
        var criterios = new List<string> { "Base (50)" };
        var v = 50;

        v += inv.Cpu.TempIdleC switch
        {
            null => Pontuar(criterios, 12, "Temperatura de repouso indisponível"),
            <= 45 => Pontuar(criterios, 25, "Temperatura de repouso <= 45 C"),
            <= 60 => Pontuar(criterios, 15, "Temperatura de repouso <= 60 C"),
            <= 75 => Pontuar(criterios, 8, "Temperatura de repouso <= 75 C"),
            _ => Pontuar(criterios, 2, "Temperatura de repouso elevada"),
        };

        v += inv.Cpu.Nucleos switch
        {
            null => Pontuar(criterios, 10, "Nº de núcleos indisponível"),
            >= 8 => Pontuar(criterios, 25, ">= 8 núcleos"),
            >= 6 => Pontuar(criterios, 18, ">= 6 núcleos"),
            >= 4 => Pontuar(criterios, 12, ">= 4 núcleos"),
            >= 2 => Pontuar(criterios, 6, ">= 2 núcleos"),
            _ => Pontuar(criterios, 3, "1 núcleo"),
        };

        return Montar(Dominio.Cpu, v, criterios);
    }

    private static Score Gpu(Inventario inv)
    {
        var criterios = new List<string>();
        if (inv.Gpu.Count == 0)
        {
            criterios.Add("Sem GPU dedicada detectada (gráficos integrados) (60)");
            return Montar(Dominio.Gpu, 60, criterios);
        }

        var v = 70;
        criterios.Add("GPU dedicada presente (70)");
        var principal = inv.Gpu[0];

        v += principal.TempIdleC switch
        {
            null => Pontuar(criterios, 10, "Temperatura de GPU indisponível"),
            <= 45 => Pontuar(criterios, 20, "Temperatura de GPU <= 45 C"),
            <= 60 => Pontuar(criterios, 12, "Temperatura de GPU <= 60 C"),
            <= 75 => Pontuar(criterios, 6, "Temperatura de GPU <= 75 C"),
            _ => Pontuar(criterios, 2, "Temperatura de GPU elevada"),
        };

        if (!string.IsNullOrWhiteSpace(principal.VersaoDriver))
        {
            v += Pontuar(criterios, 10, "Driver de vídeo identificado");
        }

        return Montar(Dominio.Gpu, v, criterios);
    }

    private static Score Ram(Inventario inv)
    {
        var criterios = new List<string> { "Base (15)" };
        var v = 15;

        var totalGb = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
        v += totalGb switch
        {
            >= 32 => Pontuar(criterios, 50, $"{totalGb} GB totais"),
            >= 16 => Pontuar(criterios, 40, $"{totalGb} GB totais"),
            >= 8 => Pontuar(criterios, 25, $"{totalGb} GB totais"),
            >= 4 => Pontuar(criterios, 12, $"{totalGb} GB totais"),
            _ => Pontuar(criterios, 5, $"{totalGb} GB totais"),
        };

        var velocidade = inv.Memoria.Count == 0 ? 0 : inv.Memoria.Max(m => m.VelocidadeMhz ?? 0);
        v += velocidade switch
        {
            0 => Pontuar(criterios, 18, "Velocidade indisponível"),
            >= 3600 => Pontuar(criterios, 35, $"{velocidade} MHz"),
            >= 3200 => Pontuar(criterios, 30, $"{velocidade} MHz"),
            >= 2666 => Pontuar(criterios, 20, $"{velocidade} MHz"),
            >= 2133 => Pontuar(criterios, 12, $"{velocidade} MHz"),
            _ => Pontuar(criterios, 8, $"{velocidade} MHz"),
        };

        return Montar(Dominio.Ram, v, criterios);
    }

    private static Score Windows(Inventario inv, ISet<Dominio> dominiosOtimizados)
    {
        var criterios = new List<string> { "Base (65)" };
        var v = 65;

        var arquitetura = inv.SistemaOperacional.Arquitetura;
        v += arquitetura is not null && arquitetura.Contains("64", StringComparison.OrdinalIgnoreCase)
            ? Pontuar(criterios, 15, "Sistema 64 bits")
            : Pontuar(criterios, 5, "Arquitetura não confirmada como 64 bits");

        v += dominiosOtimizados.Contains(Dominio.Windows)
            ? Pontuar(criterios, 20, "Otimizações de sistema aplicadas")
            : Pontuar(criterios, 0, "Sem otimizações de sistema aplicadas");

        return Montar(Dominio.Windows, v, criterios);
    }

    private static Score Estabilidade(IReadOnlyList<ResultadoValidacao> validacoes)
    {
        if (validacoes.Count == 0)
        {
            return Montar(Dominio.Estabilidade, 70, new[] { "Nenhum teste de estresse executado (70)" });
        }

        if (validacoes.Any(v => v.Regressao))
        {
            return Montar(Dominio.Estabilidade, 30, new[] { "Regressão detectada em ao menos uma categoria (30)" });
        }

        var todasValidadas = validacoes.All(
            v => string.Equals(v.Estabilidade, "Totalmente validado", StringComparison.OrdinalIgnoreCase));

        return todasValidadas
            ? Montar(Dominio.Estabilidade, 100, new[] { "Todas as categorias totalmente validadas (100)" })
            : Montar(Dominio.Estabilidade, 75, new[] { "Validado com ressalvas (75)" });
    }

    private static Score Hardware(Score cpu, Score gpu, Score ram, Score bios)
    {
        var media = (int)Math.Round((cpu.Valor + gpu.Valor + ram.Valor + bios.Valor) / 4.0);
        var criterios = new[]
        {
            $"Média de CPU ({cpu.Valor}), GPU ({gpu.Valor}), RAM ({ram.Valor}) e BIOS ({bios.Valor})",
        };
        return Montar(Dominio.Hardware, media, criterios);
    }

    private static int NotaFinal(IReadOnlyList<Score> scores)
    {
        double soma = 0;
        double pesoTotal = 0;
        foreach (var score in scores)
        {
            if (Pesos.TryGetValue(score.Dominio, out var peso))
            {
                soma += score.Valor * peso;
                pesoTotal += peso;
            }
        }

        return pesoTotal == 0 ? 0 : (int)Math.Round(soma / pesoTotal);
    }

    /// <summary>Registra o critério e devolve os pontos, para uso fluente nas somas.</summary>
    private static int Pontuar(List<string> criterios, int pontos, string descricao)
    {
        criterios.Add(string.Create(
            CultureInfo.InvariantCulture, $"{descricao} (+{pontos})"));
        return pontos;
    }

    private static Score Montar(Dominio dominio, int valor, IReadOnlyList<string> criterios)
    {
        var limitado = Math.Clamp(valor, 0, 100);
        return new Score
        {
            Dominio = dominio,
            Valor = limitado,
            Classificacao = Score.Classificar(limitado),
            Criterios = criterios,
        };
    }
}
