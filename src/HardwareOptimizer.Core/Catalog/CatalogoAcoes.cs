using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Conjunto fechado e versionado de ações de otimização. Nenhuma ação fora deste
/// catálogo pode ser executada. O LLM só pode referenciar IDs aqui presentes.
/// </summary>
public sealed class CatalogoAcoes
{
    private readonly IReadOnlyDictionary<string, AcaoOtimizacao> _acoes;

    public CatalogoAcoes(string versao, IEnumerable<AcaoOtimizacao> acoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentNullException.ThrowIfNull(acoes);

        Versao = versao;
        var mapa = new Dictionary<string, AcaoOtimizacao>(StringComparer.OrdinalIgnoreCase);
        foreach (var acao in acoes)
        {
            if (!mapa.TryAdd(acao.Id, acao))
            {
                throw new ArgumentException($"Id de ação duplicado no catálogo: '{acao.Id}'.", nameof(acoes));
            }
        }

        _acoes = mapa;
    }

    public string Versao { get; }

    public IReadOnlyCollection<AcaoOtimizacao> Todas => (IReadOnlyCollection<AcaoOtimizacao>)_acoes.Values;

    public bool Contem(string acaoId) => acaoId is not null && _acoes.ContainsKey(acaoId);

    public AcaoOtimizacao? Obter(string acaoId) =>
        acaoId is not null && _acoes.TryGetValue(acaoId, out var acao) ? acao : null;

    public IEnumerable<AcaoOtimizacao> PorCategoria(CategoriaAcao categoria) =>
        _acoes.Values.Where(a => a.Categoria == categoria);

    /// <summary>Valida a coerência de todas as ações (usado em testes e no startup).</summary>
    public Resultado VerificarCoerencia()
    {
        var erros = new List<string>();
        foreach (var acao in _acoes.Values)
        {
            var r = acao.VerificarCoerencia();
            if (r.Falha)
            {
                erros.AddRange(r.Erros);
            }
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }
}
