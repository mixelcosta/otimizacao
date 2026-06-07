using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Guarda determinística do catálogo. Recusa qualquer ação fora do catálogo e
/// qualquer valor acima do limite absoluto. É o ponto único por onde toda
/// seleção do LLM precisa passar antes de virar execução.
/// </summary>
public sealed class ValidadorAcao
{
    private readonly CatalogoAcoes _catalogo;

    public ValidadorAcao(CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _catalogo = catalogo;
    }

    public ResultadoValidacaoAcao Validar(
        string acaoId,
        IReadOnlyDictionary<string, string> parametros,
        TipoPerfil perfil)
    {
        ArgumentNullException.ThrowIfNull(parametros);

        var acao = _catalogo.Obter(acaoId);
        if (acao is null)
        {
            return new ResultadoValidacaoAcao(
                acaoId,
                acaoConhecida: false,
                parametros: Array.Empty<ResultadoParametro>(),
                erros: new[] { $"Ação '{acaoId}' não consta no catálogo whitelisted." });
        }

        var erros = new List<string>();

        // Parâmetros informados que a ação não declara são rejeitados (catálogo fechado).
        foreach (var nome in parametros.Keys)
        {
            if (acao.ObterParametro(nome) is null)
            {
                erros.Add($"Parâmetro desconhecido '{nome}' para a ação '{acaoId}'.");
            }
        }

        // Todo parâmetro declarado precisa de um valor válido.
        var resultados = new List<ResultadoParametro>();
        foreach (var parametro in acao.Parametros)
        {
            if (!parametros.TryGetValue(parametro.Nome, out var valor))
            {
                erros.Add($"Parâmetro obrigatório '{parametro.Nome}' não foi fornecido para '{acaoId}'.");
                continue;
            }

            resultados.Add(parametro.Validar(valor, perfil));
        }

        return new ResultadoValidacaoAcao(acaoId, acaoConhecida: true, resultados, erros);
    }
}
