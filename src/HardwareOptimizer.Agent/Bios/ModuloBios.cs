using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Bios;

/// <summary>
/// Orquestra o fluxo de BIOS (fluxo_bios): identifica a versão atual, consulta o
/// fabricante (via <see cref="IProvedorInfoBios"/>), decide de forma conservadora
/// e gera o guia passo a passo. NÃO aplica nada — é orientação ao usuário.
/// </summary>
public sealed class ModuloBios
{
    private readonly IProvedorInfoBios _provedor;
    private readonly AnalisadorBios _analisador = new();
    private readonly GeradorGuiaBios _gerador = new();
    private readonly ILogger _log;

    public ModuloBios(IProvedorInfoBios? provedor = null, ILogger? logger = null)
    {
        _provedor = provedor ?? new BancoCuradoBios();
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<RelatorioBios> AnalisarAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var identificacao = IdentificacaoBios.DeInventario(inventario);
        _log.LogInformation(
            "BIOS: analisando '{Fabricante} {Modelo}' (versão atual {Versao}, chave '{Chave}').",
            identificacao.Fabricante, identificacao.Modelo,
            identificacao.VersaoAtual ?? "n/d", identificacao.ChaveBusca);

        var info = await _provedor.ObterAsync(identificacao.ChaveBusca, cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            _log.LogWarning(
                "BIOS: nenhuma fonte encontrada para '{Chave}'; recomendação conservadora (manter).",
                identificacao.ChaveBusca);
        }

        var decisao = _analisador.Decidir(identificacao, info);
        _log.LogInformation(
            "BIOS: decisão -> recomenda atualizar={Recomenda} (versão recomendada {Recomendada}, risco {Risco}).",
            decisao.RecomendaAtualizar, decisao.VersaoRecomendada ?? "n/d", decisao.Risco);

        var guia = _gerador.Gerar(identificacao);

        return new RelatorioBios
        {
            Identificacao = identificacao,
            InfoFabricante = info,
            Decisao = decisao,
            Guia = guia,
        };
    }
}
