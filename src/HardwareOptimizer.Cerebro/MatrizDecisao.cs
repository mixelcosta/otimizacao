using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Cerebro;

/// <summary>Origem da proposta: modelo local (offline) ou LLM na nuvem.</summary>
public enum OrigemDecisao
{
    Local = 0,
    Nuvem = 1,
}

/// <summary>
/// Um item priorizado da matriz de decisão. Referencia sempre um ID de ação do
/// catálogo; os parâmetros já passaram pelo guard e estão dentro da faixa segura.
/// </summary>
public sealed record ItemDecisao
{
    public required string AcaoId { get; init; }

    /// <summary>1 = mais prioritário.</summary>
    public required int Prioridade { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    public required NivelRisco Risco { get; init; }

    public string? GanhoEsperado { get; init; }

    public required string Justificativa { get; init; }

    public IReadOnlyDictionary<string, string> Parametros { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Matriz de decisão produzida pelo cérebro: lista priorizada de ações do
/// catálogo, com a origem (local/nuvem) e avisos do guard (ex.: itens descartados
/// por não constarem no catálogo). É o contrato "proposta" do documento.
/// </summary>
public sealed record MatrizDecisao
{
    public required OrigemDecisao Origem { get; init; }

    /// <summary>Modelo usado (quando origem é nuvem); nulo para local.</summary>
    public string? Modelo { get; init; }

    public required IReadOnlyList<ItemDecisao> Itens { get; init; }

    /// <summary>Avisos do guard: itens fora do catálogo, parâmetros corrigidos, etc.</summary>
    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

    public IEnumerable<string> AcaoIds => Itens.Select(i => i.AcaoId);
}
