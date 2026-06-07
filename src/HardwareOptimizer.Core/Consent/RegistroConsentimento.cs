namespace HardwareOptimizer.Core.Consent;

/// <summary>
/// Resposta do usuário ao diálogo de consentimento: quais checkboxes marcou e se
/// acionou a confirmação final (botão "Confirmar alteração / Go").
/// </summary>
public sealed class RespostaConsentimento
{
    public RespostaConsentimento(IEnumerable<string> checkboxesMarcados, bool confirmacaoFinal)
    {
        CheckboxesMarcados = new HashSet<string>(checkboxesMarcados, StringComparer.OrdinalIgnoreCase);
        ConfirmacaoFinal = confirmacaoFinal;
    }

    public IReadOnlySet<string> CheckboxesMarcados { get; }

    /// <summary>Usuário acionou o botão "Confirmar alteração".</summary>
    public bool ConfirmacaoFinal { get; }
}

/// <summary>
/// Registro de auditoria do consentimento, para rastreabilidade. Guarda
/// data/hora, perfil, valores escolhidos e a versão do catálogo.
/// </summary>
public sealed record RegistroConsentimento
{
    public required string NomePerfil { get; init; }

    public required string VersaoCatalogo { get; init; }

    public DateTimeOffset RegistradoEm { get; init; } = DateTimeOffset.UtcNow;

    public required IReadOnlyList<string> CheckboxesMarcados { get; init; }

    /// <summary>Pares "AcaoId.parametro = valor" escolhidos pelo usuário.</summary>
    public required IReadOnlyList<string> ValoresEscolhidos { get; init; }
}
