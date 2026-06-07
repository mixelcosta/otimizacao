using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cli;

/// <summary>
/// Stand-in determinístico do cérebro (LLM) para o MVP. Cumpre exatamente o
/// papel previsto no documento: seleciona IDs de ações do catálogo whitelisted,
/// define parâmetros dentro das faixas e justifica — sem nunca gerar o comando
/// interno. A integração com um LLM real entra na Fase 4 do roadmap.
/// </summary>
public sealed class CerebroSimulado
{
    private readonly CatalogoAcoes _catalogo;

    public CerebroSimulado(CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _catalogo = catalogo;
    }

    /// <summary>
    /// Prioriza ações de baixo risco aplicáveis ao inventário (perfil seguro).
    /// Sempre referencia ações existentes no catálogo.
    /// </summary>
    public IReadOnlyList<Recomendacao> Recomendar(Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var ids = new List<string>
        {
            "PWR_PLANO_ALTO_DESEMPENHO",
            "PWR_USB_SUSPENSAO_SELETIVA",
            "SO_EFEITOS_VISUAIS_DESEMPENHO",
            "SO_SYSTEM_RESPONSIVENESS",
        };

        // Só recomenda ações de GPU se houver placa de vídeo detectada.
        if (inventario.Gpu.Count > 0)
        {
            ids.Add("GPU_HAGS");
        }

        var recomendacoes = new List<Recomendacao>();
        foreach (var id in ids)
        {
            var acao = _catalogo.Obter(id);
            if (acao is null)
            {
                continue; // Disciplina: nunca propor algo fora do catálogo.
            }

            recomendacoes.Add(new Recomendacao
            {
                Categoria = acao.Categoria.ToString(),
                AcaoId = acao.Id,
                Acao = acao.Titulo,
                Justificativa = acao.Descricao,
                Risco = acao.Risco,
                GanhoEsperado = EstimarGanho(acao.Risco),
                Fonte = "catálogo interno " + _catalogo.Versao,
            });
        }

        return recomendacoes;
    }

    private static string EstimarGanho(NivelRisco risco) => risco switch
    {
        NivelRisco.Nenhum or NivelRisco.MuitoBaixo => "Baixo",
        NivelRisco.Baixo => "Médio",
        _ => "Médio",
    };
}
