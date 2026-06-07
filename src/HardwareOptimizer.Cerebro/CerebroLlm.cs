using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Cérebro baseado em LLM. Monta os prompts a partir do inventário sanitizado,
/// chama o <see cref="IClienteLlm"/> e passa a resposta pelo guard, que valida
/// tudo contra o catálogo. Antes de enviar, recusa qualquer inventário que ainda
/// contenha dados pessoais (defesa de privacidade em profundidade).
/// </summary>
public sealed class CerebroLlm : ICerebro
{
    private readonly IClienteLlm _cliente;
    private readonly ConstrutorPrompt _construtor = new();
    private readonly LeitorRespostaCerebro _guard = new();
    private readonly ILogger _log;

    public CerebroLlm(IClienteLlm cliente, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        _cliente = cliente;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<MatrizDecisao> ProporAsync(
        Inventario inventarioSanitizado, CatalogoAcoes catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventarioSanitizado);
        ArgumentNullException.ThrowIfNull(catalogo);

        GarantirSanitizado(inventarioSanitizado);

        var sistema = _construtor.MontarSistema(catalogo);
        var usuario = _construtor.MontarUsuario(inventarioSanitizado, catalogo);

        _log.LogInformation("Cérebro (nuvem/{Modelo}): solicitando proposta.", _cliente.Modelo);
        var resposta = await _cliente.ResponderAsync(sistema, usuario, cancellationToken).ConfigureAwait(false);

        var matriz = _guard.Ler(resposta, catalogo, OrigemDecisao.Nuvem, _cliente.Modelo);
        _log.LogInformation(
            "Cérebro: {Itens} ação(ões) válidas após o guard; {Avisos} aviso(s).",
            matriz.Itens.Count, matriz.Avisos.Count);

        return matriz;
    }

    /// <summary>
    /// Recusa enviar à nuvem se o inventário ainda tiver PII (nomes, chave de
    /// produto). Após o pipeline de sanitização, esses campos são nulos.
    /// </summary>
    private static void GarantirSanitizado(Inventario inventario)
    {
        if (inventario.Identificadores is { } id
            && (NaoVazio(id.NomeUsuario) || NaoVazio(id.NomeMaquina) || NaoVazio(id.ChaveProdutoWindows)))
        {
            throw new InvalidOperationException(
                "Envio recusado: o inventário ainda contém dados pessoais não sanitizados. "
                + "Passe pelo pipeline de sanitização antes de enviar ao cérebro na nuvem.");
        }
    }

    private static bool NaoVazio(string? valor) => !string.IsNullOrWhiteSpace(valor);
}
