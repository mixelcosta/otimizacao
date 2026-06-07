using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;

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

    public ModuloBios(IProvedorInfoBios? provedor = null)
    {
        _provedor = provedor ?? new BancoCuradoBios();
    }

    public async Task<RelatorioBios> AnalisarAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var identificacao = IdentificacaoBios.DeInventario(inventario);
        var info = await _provedor.ObterAsync(identificacao.ChaveBusca, cancellationToken).ConfigureAwait(false);
        var decisao = _analisador.Decidir(identificacao, info);
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
