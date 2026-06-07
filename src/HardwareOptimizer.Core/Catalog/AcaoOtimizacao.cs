using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Ação de otimização pré-construída, auditada e parametrizada (entrada do
/// catálogo whitelisted). O LLM seleciona o <see cref="Id"/> e define parâmetros
/// dentro das faixas; o agente determinístico executa o <see cref="ComandoInternoId"/>.
/// </summary>
public sealed class AcaoOtimizacao
{
    public required string Id { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    public required string Titulo { get; init; }

    public required string Descricao { get; init; }

    public IReadOnlyList<Parametro> Parametros { get; init; } = Array.Empty<Parametro>();

    /// <summary>
    /// Identificador do comando interno determinístico e versionado que aplica a
    /// ação. NUNCA é fornecido pelo LLM; resolvido pelo agente local.
    /// </summary>
    public required string ComandoInternoId { get; init; }

    /// <summary>Descrição da ação inversa correspondente, usada no rollback.</summary>
    public required string Reversao { get; init; }

    public required NivelRisco Risco { get; init; }

    public bool RequerAprovacao { get; init; } = true;

    public bool RequerReinicio { get; init; }

    /// <summary>Checagens obrigatórias antes de aplicar (ex.: backup_confirmado).</summary>
    public IReadOnlyList<string> PreCondicoes { get; init; } = Array.Empty<string>();

    public Parametro? ObterParametro(string nome) =>
        Parametros.FirstOrDefault(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>Valida a coerência interna de todos os parâmetros desta ação.</summary>
    public Resultado VerificarCoerencia()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            erros.Add("Ação sem Id.");
        }

        if (string.IsNullOrWhiteSpace(ComandoInternoId))
        {
            erros.Add($"Ação '{Id}' sem comando interno associado.");
        }

        foreach (var parametro in Parametros)
        {
            var coerencia = parametro.VerificarCoerencia();
            if (coerencia.Falha)
            {
                erros.AddRange(coerencia.Erros);
            }
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }
}
