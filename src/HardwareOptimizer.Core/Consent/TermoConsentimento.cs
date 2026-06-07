namespace HardwareOptimizer.Core.Consent;

/// <summary>Item de aceite obrigatório no diálogo de consentimento.</summary>
public sealed record Checkbox(string Id, string Texto, bool Obrigatorio = true);

/// <summary>
/// Termo de consentimento exibido ao salvar/aplicar um perfil customizado.
/// Reproduz <c>fluxo_consentimento_customizado</c> do documento: aviso de
/// responsabilidade + dois checkboxes obrigatórios.
/// </summary>
public sealed class TermoConsentimento
{
    public const string IdAceiteRiscos = "aceite_riscos";
    public const string IdDesejoProsseguir = "desejo_prosseguir";

    public TermoConsentimento(string titulo, IReadOnlyList<string> corpoAviso, IReadOnlyList<Checkbox> checkboxes)
    {
        Titulo = titulo;
        CorpoAviso = corpoAviso;
        Checkboxes = checkboxes;
    }

    public string Titulo { get; }

    public IReadOnlyList<string> CorpoAviso { get; }

    public IReadOnlyList<Checkbox> Checkboxes { get; }

    public IReadOnlyList<string> CheckboxesObrigatorios =>
        Checkboxes.Where(c => c.Obrigatorio).Select(c => c.Id).ToList();

    /// <summary>Termo padrão, com os textos definidos no documento de arquitetura.</summary>
    public static TermoConsentimento Padrao() => new(
        titulo: "Aviso de responsabilidade - parametrização manual",
        corpoAviso: new[]
        {
            "Você está definindo valores manualmente, fora do perfil seguro recomendado pelo sistema.",
            "Esses valores NÃO foram validados pelo sistema e podem causar instabilidade, travamentos, "
                + "tela azul, perda de dados, superaquecimento ou, em casos extremos, dano ao hardware.",
            "Parâmetros fora da faixa segura podem afetar garantia e estabilidade.",
            "A responsabilidade pela escolha dos valores e configurações é inteiramente sua.",
            "Recomendamos manter o backup gerado pelo sistema antes de prosseguir e validar com "
                + "testes de estresse após aplicar.",
        },
        checkboxes: new[]
        {
            new Checkbox(IdAceiteRiscos, "Li e aceito os riscos de parametrizar as configurações manualmente."),
            new Checkbox(IdDesejoProsseguir, "Desejo prosseguir com as modificações."),
        });
}
