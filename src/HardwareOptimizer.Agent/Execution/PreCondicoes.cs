using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>Contexto de execução: estado que as pré-condições consultam.</summary>
public sealed class ContextoExecucao
{
    public required bool BackupConfirmado { get; init; }
}

/// <summary>Verifica as pré-condições obrigatórias de uma ação antes de aplicá-la.</summary>
public interface IVerificadorPreCondicoes
{
    Resultado Verificar(AcaoOtimizacao acao, IReadOnlyDictionary<string, string> parametros, ContextoExecucao contexto);
}

/// <summary>
/// Implementação padrão. Conhece um conjunto fechado de pré-condições e recusa
/// qualquer pré-condição desconhecida (postura conservadora).
/// </summary>
public sealed class VerificadorPreCondicoes : IVerificadorPreCondicoes
{
    public Resultado Verificar(
        AcaoOtimizacao acao, IReadOnlyDictionary<string, string> parametros, ContextoExecucao contexto)
    {
        ArgumentNullException.ThrowIfNull(acao);
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(contexto);

        var erros = new List<string>();

        foreach (var preCondicao in acao.PreCondicoes)
        {
            switch (preCondicao)
            {
                case "backup_confirmado":
                    if (!contexto.BackupConfirmado)
                    {
                        erros.Add($"Ação '{acao.Id}': backup não confirmado. Sem backup, não se prossegue.");
                    }

                    break;

                case "servico_consta_na_lista_segura":
                    VerificarServicoNaListaSegura(acao, parametros, erros);
                    break;

                default:
                    erros.Add($"Ação '{acao.Id}': pré-condição desconhecida '{preCondicao}' (bloqueio conservador).");
                    break;
            }
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }

    private static void VerificarServicoNaListaSegura(
        AcaoOtimizacao acao, IReadOnlyDictionary<string, string> parametros, List<string> erros)
    {
        if (acao.ObterParametro("nome_servico") is not ParametroListaBranca lista)
        {
            erros.Add($"Ação '{acao.Id}': parâmetro 'nome_servico' de lista branca ausente.");
            return;
        }

        if (!parametros.TryGetValue("nome_servico", out var nome) ||
            !lista.ValoresSeguros.Contains(nome, StringComparer.OrdinalIgnoreCase))
        {
            erros.Add($"Ação '{acao.Id}': serviço '{(parametros.GetValueOrDefault("nome_servico") ?? "?")}' "
                + "não consta na lista segura.");
        }
    }
}
