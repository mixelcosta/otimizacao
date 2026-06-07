using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Recomendação proposta pelo cérebro (contrato "recomendacao"). O LLM apenas
/// seleciona e prioriza ações do catálogo; nunca gera o comando interno.
/// </summary>
public sealed record Recomendacao
{
    public required string Categoria { get; init; }

    /// <summary>Identificador da ação no catálogo whitelisted que materializa esta recomendação.</summary>
    public string? AcaoId { get; init; }

    public string? ValorAtual { get; init; }

    public string? ValorRecomendado { get; init; }

    public required string Acao { get; init; }

    public required string Justificativa { get; init; }

    public NivelRisco Risco { get; init; }

    public string? GanhoEsperado { get; init; }

    /// <summary>Fonte sempre visível (exigência do documento para verificação com fabricante).</summary>
    public string? Fonte { get; init; }

    public IReadOnlyList<string> PassosUsuario { get; init; } = Array.Empty<string>();
}
