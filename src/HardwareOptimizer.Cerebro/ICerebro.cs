using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// O cérebro: propõe uma matriz de decisão a partir do inventário
/// <b>já sanitizado</b> e do catálogo. Implementações: local (offline) e LLM.
/// O cérebro NUNCA gera comandos — apenas seleciona e prioriza IDs do catálogo.
/// </summary>
public interface ICerebro
{
    Task<MatrizDecisao> ProporAsync(
        Inventario inventarioSanitizado,
        CatalogoAcoes catalogo,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstração mínima de um modelo de linguagem: recebe prompt de sistema + de
/// usuário e devolve texto. Mantém o cérebro independente de provedor e
/// permite testar com um cliente falso.
/// </summary>
public interface IClienteLlm
{
    /// <summary>Identificação do modelo, para registrar na matriz.</summary>
    string Modelo { get; }

    Task<string> ResponderAsync(
        string promptSistema, string promptUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Responde mantendo histórico de conversa. Cada entrada é (role, conteúdo)
    /// onde role é "user" ou "assistant". A última mensagem deve ser do usuário.
    /// </summary>
    Task<string> ResponderConversaAsync(
        string promptSistema,
        IReadOnlyList<(string Role, string Conteudo)> historico,
        CancellationToken cancellationToken = default);
}
