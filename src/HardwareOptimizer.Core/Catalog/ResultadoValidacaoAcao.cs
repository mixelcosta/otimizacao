using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>Resultado da validação de uma seleção de ação (id + parâmetros) contra o catálogo.</summary>
public sealed class ResultadoValidacaoAcao
{
    public ResultadoValidacaoAcao(
        string acaoId,
        bool acaoConhecida,
        IReadOnlyList<ResultadoParametro> parametros,
        IReadOnlyList<string> erros)
    {
        AcaoId = acaoId;
        AcaoConhecida = acaoConhecida;
        Parametros = parametros;
        Erros = erros;
    }

    public string AcaoId { get; }

    /// <summary>A ação consta no catálogo whitelisted?</summary>
    public bool AcaoConhecida { get; }

    public IReadOnlyList<ResultadoParametro> Parametros { get; }

    public IReadOnlyList<string> Erros { get; }

    /// <summary>Algum parâmetro foi barrado pelo limite absoluto (bloqueio rígido).</summary>
    public bool TemBloqueioRigido =>
        Parametros.Any(p => p.Situacao == SituacaoParametro.BloqueioLimiteAbsoluto);

    /// <summary>Algum parâmetro está fora da faixa segura (risco assumido) e exige consentimento.</summary>
    public bool ExigeConsentimento => Parametros.Any(p => p.ExigeConsentimento);

    /// <summary>A ação pode ser aplicada: está no catálogo, sem erros e com todos os parâmetros aplicáveis.</summary>
    public bool Aplicavel =>
        AcaoConhecida && Erros.Count == 0 && Parametros.All(p => p.Aplicavel);

    public Resultado ComoResultado() => Aplicavel ? Resultado.Ok() : Resultado.Falhar(ReunirErros());

    private IReadOnlyList<string> ReunirErros()
    {
        var erros = new List<string>(Erros);
        if (!AcaoConhecida)
        {
            erros.Add($"Ação '{AcaoId}' não consta no catálogo whitelisted.");
        }

        erros.AddRange(
            Parametros.Where(p => !p.Aplicavel).Select(p => $"{p.Parametro}: {p.Mensagem}"));

        return erros;
    }
}
