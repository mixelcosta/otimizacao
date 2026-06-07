using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Valida a estabilidade após aplicar uma categoria. É o ponto de extensão para
/// o Runner de Validação (OCCT, Cinebench, Prime95, MemTest86) da Fase 9. Se a
/// validação reprovar, o executor reverte a categoria.
/// </summary>
public interface IValidadorCategoria
{
    Task<ResultadoValidacao> ValidarAsync(
        CategoriaAcao categoria,
        IReadOnlyList<RegistroAlteracao> alteracoes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validador trivial do MVP: considera toda categoria estável. Substituível por
/// uma implementação que dispare testes de estresse reais.
/// </summary>
public sealed class ValidadorCategoriaSempreEstavel : IValidadorCategoria
{
    public Task<ResultadoValidacao> ValidarAsync(
        CategoriaAcao categoria,
        IReadOnlyList<RegistroAlteracao> alteracoes,
        CancellationToken cancellationToken = default)
    {
        var resultado = new ResultadoValidacao
        {
            Categoria = categoria.ToString(),
            Ferramenta = "validação-mvp",
            Regressao = false,
            Estabilidade = "Totalmente validado",
        };

        return Task.FromResult(resultado);
    }
}
