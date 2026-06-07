using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Decisão conservadora sobre atualizar a BIOS. Recomenda atualização apenas
/// quando há ganho real de estabilidade/compatibilidade e a versão atual é mais
/// antiga. Atualização de BIOS é arriscada por natureza: o risco nunca é menor
/// que Médio quando há flash envolvido.
/// </summary>
public sealed class AnalisadorBios
{
    public DecisaoBios Decidir(IdentificacaoBios identificacao, InfoBiosFabricante? info)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        if (info is null)
        {
            return new DecisaoBios
            {
                RecomendaAtualizar = false,
                Ganho = GanhoEstimado.Nenhum,
                Risco = NivelRisco.Medio,
                Justificativa =
                    "Sem informação do fabricante para este modelo. Mantenha a versão atual e "
                    + "verifique manualmente a página oficial de suporte.",
                VersaoAtual = identificacao.VersaoAtual,
            };
        }

        var comparacao = VersaoBios.Comparar(identificacao.VersaoAtual, info.VersaoMaisRecente);

        if (comparacao >= 0)
        {
            return new DecisaoBios
            {
                RecomendaAtualizar = false,
                Ganho = GanhoEstimado.Nenhum,
                Risco = NivelRisco.Nenhum,
                Justificativa = "A BIOS já está na versão mais recente conhecida (ou superior).",
                Fonte = info.Fonte,
                VersaoAtual = identificacao.VersaoAtual,
                VersaoRecomendada = info.VersaoMaisRecente,
            };
        }

        // Há versão mais nova, mas sem ganho real: postura conservadora — não recomenda.
        if (info.Ganho == GanhoEstimado.Nenhum)
        {
            return new DecisaoBios
            {
                RecomendaAtualizar = false,
                Ganho = GanhoEstimado.Nenhum,
                Risco = NivelRisco.Medio,
                Justificativa =
                    "Existe versão mais recente, porém sem ganho real de estabilidade ou "
                    + "compatibilidade. Atualização não recomendada (risco sem benefício claro).",
                Fonte = info.Fonte,
                VersaoAtual = identificacao.VersaoAtual,
                VersaoRecomendada = info.VersaoMaisRecente,
            };
        }

        return new DecisaoBios
        {
            RecomendaAtualizar = true,
            Ganho = info.Ganho,
            Risco = NivelRisco.Medio,
            Justificativa = info.Motivo
                ?? "Atualização recomendada por ganho de estabilidade/compatibilidade.",
            Fonte = info.Fonte,
            VersaoAtual = identificacao.VersaoAtual,
            VersaoRecomendada = info.VersaoMaisRecente,
        };
    }
}
