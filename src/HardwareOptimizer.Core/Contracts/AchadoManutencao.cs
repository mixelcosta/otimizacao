namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Achado de manutenção física sinalizado por <c>DetectorPastaTermica</c>
/// (Features.Manutencao, spec-2-1) — mesmo estilo de <see cref="InfoSoftware"/>/
/// <see cref="InfoBios"/>: só existe quando a leitura de sensor sinaliza suspeita
/// real (guard anti-alucinação). Sem sinal, nenhuma instância é criada.
/// </summary>
public sealed record AchadoManutencao
{
    /// <summary>Temperatura máxima lida em repouso (idle), em °C.</summary>
    public required double TemperaturaIdleC { get; init; }

    /// <summary>
    /// Temperatura máxima lida sob carga simulada, em °C — contexto para a
    /// Story 2.3 comparar antes/depois; não participa da regra de decisão
    /// desta história (ver Design Notes da spec-2-1). <c>null</c> quando a
    /// leitura sob carga não teve sensor de temperatura disponível ou finito —
    /// nunca um substituto fabricado (corrigido na revisão independente).
    /// </summary>
    public double? TemperaturaCargaC { get; init; }

    public required Custo Custo { get; init; }

    public required string Justificativa { get; init; }
}
