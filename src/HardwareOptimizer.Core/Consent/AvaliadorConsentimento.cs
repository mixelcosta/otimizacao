using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Core.Consent;

/// <summary>
/// Aplica as regras do diálogo de consentimento: o botão "Confirmar alteração"
/// só é válido com todos os checkboxes obrigatórios marcados e a confirmação
/// final acionada. Produz o registro de auditoria correspondente.
/// </summary>
public sealed class AvaliadorConsentimento
{
    private readonly TermoConsentimento _termo;
    private readonly ILogger _log;

    public AvaliadorConsentimento(TermoConsentimento? termo = null, ILogger? logger = null)
    {
        _termo = termo ?? TermoConsentimento.Padrao();
        _log = logger ?? NullLogger.Instance;
    }

    public TermoConsentimento Termo => _termo;

    /// <summary>
    /// Regra de habilitação do botão "Go": todos os checkboxes obrigatórios marcados.
    /// </summary>
    public bool PodeHabilitarConfirmacao(IEnumerable<string> checkboxesMarcados)
    {
        ArgumentNullException.ThrowIfNull(checkboxesMarcados);

        var obrigatorios = _termo.CheckboxesObrigatorios;
        if (obrigatorios.Count == 0)
        {
            // Termo sem aceites obrigatórios não habilita a confirmação (postura conservadora).
            return false;
        }

        var marcados = new HashSet<string>(checkboxesMarcados, StringComparer.OrdinalIgnoreCase);
        return obrigatorios.All(marcados.Contains);
    }

    /// <summary>
    /// Avalia a resposta completa. Em caso de sucesso, devolve o registro de
    /// auditoria pronto para persistir. Não muta o perfil; o chamador decide
    /// marcar <see cref="Perfil.ConsentimentoRegistrado"/>.
    /// </summary>
    public Resultado<RegistroConsentimento> Avaliar(
        RespostaConsentimento resposta, Perfil perfil, string versaoCatalogo)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        ArgumentNullException.ThrowIfNull(perfil);

        if (!PodeHabilitarConfirmacao(resposta.CheckboxesMarcados))
        {
            _log.LogWarning(
                "Consentimento RECUSADO para o perfil '{Perfil}': checkboxes obrigatórios não marcados.",
                perfil.Nome);
            return Resultado<RegistroConsentimento>.Falhar(
                "Consentimento incompleto: é necessário marcar todos os checkboxes obrigatórios.");
        }

        if (!resposta.ConfirmacaoFinal)
        {
            _log.LogWarning(
                "Consentimento RECUSADO para o perfil '{Perfil}': confirmação final não acionada.", perfil.Nome);
            return Resultado<RegistroConsentimento>.Falhar(
                "Confirmação final não acionada: o usuário não confirmou a alteração.");
        }

        var registro = new RegistroConsentimento
        {
            NomePerfil = perfil.Nome,
            VersaoCatalogo = versaoCatalogo,
            CheckboxesMarcados = resposta.CheckboxesMarcados.ToList(),
            ValoresEscolhidos = AchatarValores(perfil),
        };

        _log.LogInformation(
            "Consentimento CONCEDIDO para o perfil '{Perfil}' (catálogo {Versao}).",
            perfil.Nome, versaoCatalogo);
        return Resultado<RegistroConsentimento>.Ok(registro);
    }

    private static IReadOnlyList<string> AchatarValores(Perfil perfil)
    {
        var linhas = new List<string>();
        foreach (var selecao in perfil.Selecoes)
        {
            if (selecao.Parametros.Count == 0)
            {
                linhas.Add(selecao.AcaoId);
                continue;
            }

            foreach (var (nome, valor) in selecao.Parametros)
            {
                linhas.Add($"{selecao.AcaoId}.{nome} = {valor}");
            }
        }

        return linhas;
    }
}
