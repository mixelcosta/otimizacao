using System.Globalization;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Manutencao;

/// <summary>
/// Detecta o sintoma clássico de pasta térmica ressecada: temperatura
/// anormalmente alta mesmo em repouso (idle) — a peça não dissipa calor com
/// eficiência mesmo sem carga. Lógica pura, sem I/O: recebe duas leituras já
/// feitas por <c>ServicoSensores</c> (idle e sob carga simulada) e decide se o
/// achado deve ser exibido. Guard anti-alucinação: sem sinal real, devolve
/// <c>null</c> — nunca "possível problema" genérico (spec-2-1, Boundaries §Always).
/// </summary>
public static class DetectorPastaTermica
{
    /// <summary>
    /// Limiar de temperatura idle (°C) acima do qual o achado é sinalizado.
    /// Critério não definido nas fontes upstream — decisão desta implementação:
    /// heurística conservadora, revisável (ver Design Notes da spec-2-1).
    /// </summary>
    public const double TemperaturaIdleSuspeitaC = 55.0;

    /// <summary>
    /// Custo estimado (BRL) de uma limpeza/reaplicação de pasta térmica —
    /// heurística de referência, sem cálculo dinâmico nesta história (mesmo
    /// espírito do valor inicial de <see cref="TemperaturaIdleSuspeitaC"/>: um
    /// placeholder documentado e revisável).
    /// </summary>
    public const decimal CustoEstimadoLimpeza = 50m;

    /// <summary>
    /// Compara a leitura idle contra <see cref="TemperaturaIdleSuspeitaC"/>. A
    /// leitura sob carga é só contexto (devolvida no achado para a Story 2.3
    /// comparar antes/depois) — não participa da regra de decisão.
    /// </summary>
    public static AchadoManutencao? Detectar(LeituraSensores idle, LeituraSensores carga)
    {
        ArgumentNullException.ThrowIfNull(idle);
        ArgumentNullException.ThrowIfNull(carga);

        var temperaturaIdle = idle.TemperaturaMaxC;
        if (temperaturaIdle is null || !double.IsFinite(temperaturaIdle.Value) || temperaturaIdle.Value < TemperaturaIdleSuspeitaC)
        {
            // Sem sensor de temperatura disponível, leitura inválida (NaN/±Infinity
            // — sensor com falha, corrigido na revisão independente: NaN < limiar
            // avalia false em IEEE754 e passava batido antes do double.IsFinite),
            // ou temperatura idle normal: nenhum sinal real de pasta térmica
            // ressecada — guard anti-alucinação.
            return null;
        }

        // A leitura sob carga é só contexto (Design Notes da spec-2-1) — quando
        // indisponível ou inválida (NaN/±Infinity), o campo fica null. Nunca um
        // substituto fabricado (fallback pro valor idle removido na revisão
        // independente: um valor copiado não é uma medição real sob carga).
        var temperaturaCarga = carga.TemperaturaMaxC is { } c && double.IsFinite(c) ? c : (double?)null;

        return new AchadoManutencao
        {
            TemperaturaIdleC = temperaturaIdle.Value,
            TemperaturaCargaC = temperaturaCarga,
            Custo = new Custo { ValorEstimado = CustoEstimadoLimpeza },
            Justificativa =
                $"Temperatura em repouso de {temperaturaIdle.Value.ToString("0.0", CultureInfo.InvariantCulture)}°C " +
                $"está acima do limiar de suspeita ({TemperaturaIdleSuspeitaC.ToString("0.0", CultureInfo.InvariantCulture)}°C) — " +
                "sintoma comum de pasta térmica ressecada ou acúmulo de poeira no dissipador.",
        };
    }
}
