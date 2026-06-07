using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Orquestra o fluxo_visao: pré-processa a imagem, monta o prompt direcionado,
/// chama o modelo multimodal e estrutura a leitura. A confirmação contra o
/// inventário fica em <see cref="ConferenciaVisual"/> (a regra do documento de
/// nunca confiar cegamente na leitura visual).
/// </summary>
public sealed class ModuloVisao
{
    private readonly IClienteVisao _cliente;
    private readonly ConstrutorPromptVisao _prompt = new();
    private readonly LeitorRespostaVisao _leitor = new();
    private readonly PreProcessadorImagem _pre = new();
    private readonly ILogger _log;

    public ModuloVisao(IClienteVisao cliente, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        _cliente = cliente;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<LeituraVisual> InterpretarAsync(
        ImagemEntrada imagem, CasoUsoVisao caso, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagem);

        foreach (var aviso in _pre.Validar(imagem))
        {
            _log.LogWarning("Visão: {Aviso}", aviso);
        }

        _log.LogInformation(
            "Visão: analisando imagem '{Descricao}' ({MediaType}) para o caso {Caso}.",
            imagem.Descricao ?? "(sem nome)", imagem.MediaType, caso);

        var sistema = _prompt.MontarSistema();
        var usuario = _prompt.MontarUsuario(caso);
        var resposta = await _cliente.AnalisarAsync(imagem, sistema, usuario, cancellationToken).ConfigureAwait(false);

        var leitura = _leitor.Ler(resposta, _cliente.Modelo);
        _log.LogInformation(
            "Visão: tela={Tipo}, confiança={Confianca}, {Campos} campo(s) lido(s).",
            leitura.TipoTela, leitura.Confianca, leitura.Campos.Count);

        return leitura;
    }
}
