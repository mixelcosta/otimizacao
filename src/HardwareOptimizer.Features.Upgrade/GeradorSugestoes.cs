using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Upgrade;

public sealed class GeradorSugestoes
{
    private readonly ValidadorCompatibilidade _validador;
    private readonly CalculadoraGargalo _gargalo;
    private readonly ILogger<GeradorSugestoes> _log;

    public GeradorSugestoes(
        ValidadorCompatibilidade validador,
        CalculadoraGargalo gargalo,
        ILogger<GeradorSugestoes> log)
    {
        _validador = validador;
        _gargalo = gargalo;
        _log = log;
    }

    public IReadOnlyList<SugestaoUpgrade> Sugerir(Inventario inventario, ModoSugestao modo)
    {
        var gargaloAtual = _gargalo.Calcular(inventario);
        _log.LogInformation("Gargalo: {Componente} ({Pct}%)", gargaloAtual.ComponenteLimitante, gargaloAtual.PorcentagemGargalo);

        var sugestoes = new List<SugestaoUpgrade>();

        if (modo == ModoSugestao.CustoBeneficio)
        {
            // RAM Dual-Channel: upgrade mais barato quando há slot livre
            if (inventario.Memoria.Count == 1)
            {
                var pente = inventario.Memoria[0];
                var novoPente = new PecaSubstituta
                {
                    Tipo = TipoPecaUpgrade.Ram,
                    Modelo = $"{pente.Fabricante ?? "DDR4"} {pente.TamanhoGb}GB {pente.VelocidadeMhz}MHz (segundo pente)",
                    TipoDdr = "DDR4",
                    VelocidadeMhz = pente.VelocidadeMhz,
                };
                var compat = _validador.Validar(inventario, novoPente);
                sugestoes.Add(new SugestaoUpgrade
                {
                    Modo = modo,
                    Peca = novoPente,
                    Compatibilidade = compat,
                    Justificativa = "Ativar Dual-Channel com segundo pente idêntico é o upgrade de melhor custo-benefício.",
                });
            }

            // GPU se CPU for o gargalo, ou vice-versa
            if (gargaloAtual.ComponenteLimitante == "GPU")
            {
                var gpuMidrange = new PecaSubstituta
                {
                    Tipo = TipoPecaUpgrade.Gpu,
                    Modelo = "NVIDIA RTX 4070",
                    TdpW = 200,
                    InterfacePcie = "PCIe 4.0 x16",
                    PrecoEstimado = 2500,
                };
                var compat = _validador.Validar(inventario, gpuMidrange);
                sugestoes.Add(new SugestaoUpgrade
                {
                    Modo = modo,
                    Peca = gpuMidrange,
                    Compatibilidade = compat,
                    Justificativa = $"GPU é o gargalo atual ({gargaloAtual.PorcentagemGargalo:F0}%); RTX 4070 é boa relação custo/ganho.",
                });
            }
        }
        else // HighEnd
        {
            // CPU high-end compatível com a placa-mãe atual
            var cpuHighEnd = new PecaSubstituta
            {
                Tipo = TipoPecaUpgrade.Cpu,
                Modelo = "Intel Core i9-14900K",
                Socket = "LGA1700",
                TdpW = 125,
            };
            var compatCpu = _validador.Validar(inventario, cpuHighEnd);
            sugestoes.Add(new SugestaoUpgrade
            {
                Modo = modo,
                Peca = cpuHighEnd,
                Compatibilidade = compatCpu,
                Justificativa = "Máxima performance para a plataforma LGA1700.",
            });

            var gpuHighEnd = new PecaSubstituta
            {
                Tipo = TipoPecaUpgrade.Gpu,
                Modelo = "NVIDIA RTX 4090",
                TdpW = 450,
                InterfacePcie = "PCIe 4.0 x16",
                PrecoEstimado = 12000,
            };
            var compatGpu = _validador.Validar(inventario, gpuHighEnd);
            sugestoes.Add(new SugestaoUpgrade
            {
                Modo = modo,
                Peca = gpuHighEnd,
                Compatibilidade = compatGpu,
                Justificativa = "GPU mais potente do mercado para máximo FPS.",
            });
        }

        return sugestoes;
    }
}
