using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Cérebro local determinístico (opção "modelo local" do documento e padrão do
/// MVP). Não envia nada à nuvem: seleciona ações de baixo risco aplicáveis ao
/// inventário, com os valores padrão seguros. Sempre produz uma matriz válida.
/// </summary>
public sealed class CerebroLocal : ICerebro
{
    public Task<MatrizDecisao> ProporAsync(
        Inventario inventarioSanitizado, CatalogoAcoes catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventarioSanitizado);
        ArgumentNullException.ThrowIfNull(catalogo);
        cancellationToken.ThrowIfCancellationRequested();

        var temGpu = inventarioSanitizado.Gpu.Count > 0;
        var itens = new List<ItemDecisao>();
        var prioridade = 1;

        foreach (var acao in catalogo.Todas
            .Where(a => a.Risco <= NivelRisco.Baixo)
            .Where(a => temGpu || a.Categoria != CategoriaAcao.Gpu)
            .OrderBy(a => (int)a.Risco)
            .ThenBy(a => a.Categoria)
            .ThenBy(a => a.Id, StringComparer.Ordinal))
        {
            var parametros = acao.Parametros.ToDictionary(
                p => p.Nome, p => p.ValorPadraoSeguro, StringComparer.OrdinalIgnoreCase);

            itens.Add(new ItemDecisao
            {
                AcaoId = acao.Id,
                Prioridade = prioridade++,
                Categoria = acao.Categoria,
                Risco = acao.Risco,
                GanhoEsperado = EstimarGanho(acao.Risco),
                Justificativa = acao.Descricao,
                Parametros = parametros,
            });
        }

        var matriz = new MatrizDecisao
        {
            Origem = OrigemDecisao.Local,
            Modelo = null,
            Itens = itens,
        };

        return Task.FromResult(matriz);
    }

    private static string EstimarGanho(NivelRisco risco) => risco switch
    {
        NivelRisco.Nenhum or NivelRisco.MuitoBaixo => "Baixo",
        _ => "Médio",
    };
}
