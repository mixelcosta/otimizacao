using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Runner de validação real: após cada categoria, executa a ferramenta de
/// estresse, parseia a saída e analisa regressão. Implementa
/// <see cref="IValidadorCategoria"/>, de modo que o <c>ExecutorControlado</c>
/// reverte automaticamente a categoria quando <see cref="ResultadoValidacao.Regressao"/>
/// é verdadeiro — fechando o ciclo validar → comparar → reverter.
/// </summary>
public sealed class RunnerValidacao : IValidadorCategoria
{
    private readonly IFerramentaEstresse _ferramenta;
    private readonly ParserEstresse _parser = new();
    private readonly AnalisadorRegressao _analisador = new();
    private readonly LimiaresValidacao _limiares;
    private readonly MedicaoEstresse? _baseline;
    private readonly TimeSpan _duracao;
    private readonly ILogger _log;

    public RunnerValidacao(
        IFerramentaEstresse ferramenta,
        LimiaresValidacao? limiares = null,
        MedicaoEstresse? baseline = null,
        TimeSpan? duracao = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(ferramenta);
        _ferramenta = ferramenta;
        _limiares = limiares ?? LimiaresValidacao.Padrao;
        _baseline = baseline;
        _duracao = duracao ?? TimeSpan.FromMinutes(1);
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<ResultadoValidacao> ValidarAsync(
        CategoriaAcao categoria,
        IReadOnlyList<RegistroAlteracao> alteracoes,
        CancellationToken cancellationToken = default)
    {
        _log.LogInformation(
            "Validação {Categoria}: executando {Ferramenta} (recomendada: {Recomendada}).",
            categoria, _ferramenta.Nome, SeletorFerramenta.Recomendada(categoria));

        var saida = await _ferramenta.ExecutarAsync(categoria, _duracao, cancellationToken).ConfigureAwait(false);
        var medicao = _parser.Parse(saida);
        var resultado = _analisador.Analisar(categoria, _ferramenta.Nome, medicao, _baseline, _limiares);

        if (resultado.Regressao)
        {
            _log.LogWarning(
                "Validação {Categoria}: REPROVADO — {Erros}", categoria, string.Join("; ", resultado.Erros));
        }
        else
        {
            _log.LogInformation("Validação {Categoria}: {Estabilidade}.", categoria, resultado.Estabilidade);
        }

        return resultado;
    }
}
