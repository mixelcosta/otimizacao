using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Desfecho da validação de um valor proposto para um parâmetro, segundo as
/// regras do documento (faixa_segura / faixa_permitida / limite_absoluto).
/// </summary>
public sealed record ResultadoParametro
{
    private ResultadoParametro(string parametro, string valor, SituacaoParametro situacao, string mensagem)
    {
        Parametro = parametro;
        Valor = valor;
        Situacao = situacao;
        Mensagem = mensagem;
    }

    public string Parametro { get; }

    public string Valor { get; }

    public SituacaoParametro Situacao { get; }

    public string Mensagem { get; }

    /// <summary>Pode ser persistido/aplicado? Falso para rejeição e bloqueio rígido.</summary>
    public bool Aplicavel => Situacao is SituacaoParametro.Aceito or SituacaoParametro.RiscoAssumido;

    /// <summary>Exige consentimento explícito por estar fora da faixa segura.</summary>
    public bool ExigeConsentimento => Situacao == SituacaoParametro.RiscoAssumido;

    public static ResultadoParametro Aceito(string parametro, string valor) =>
        new(parametro, valor, SituacaoParametro.Aceito, "Dentro da faixa segura.");

    public static ResultadoParametro RiscoAssumido(string parametro, string valor, string detalhe) =>
        new(parametro, valor, SituacaoParametro.RiscoAssumido,
            "Fora da faixa segura, dentro da permitida — risco assumido pelo usuário. " + detalhe);

    public static ResultadoParametro Rejeitado(string parametro, string valor, string motivo) =>
        new(parametro, valor, SituacaoParametro.Rejeitado, motivo);

    public static ResultadoParametro BloqueioLimiteAbsoluto(string parametro, string valor, string detalhe) =>
        new(parametro, valor, SituacaoParametro.BloqueioLimiteAbsoluto,
            "Bloqueio rígido: ultrapassa o limite absoluto. " + detalhe);
}
