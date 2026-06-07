using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Comando interno que define um único alvo do <see cref="IEstadoSistema"/> com
/// um valor derivado dos parâmetros. Cobre toggles (valor fixo) e ações
/// parametrizadas (valor vindo de um parâmetro), com rollback por restauração
/// do valor anterior.
/// </summary>
public sealed class ComandoEstadoSistema : IComandoInterno
{
    private readonly IEstadoSistema _estado;
    private readonly Func<IReadOnlyDictionary<string, string>, string> _resolverAlvo;
    private readonly Func<IReadOnlyDictionary<string, string>, string> _resolverValor;

    public ComandoEstadoSistema(
        string id,
        IEstadoSistema estado,
        Func<IReadOnlyDictionary<string, string>, string> resolverAlvo,
        Func<IReadOnlyDictionary<string, string>, string> resolverValor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(resolverAlvo);
        ArgumentNullException.ThrowIfNull(resolverValor);

        Id = id;
        _estado = estado;
        _resolverAlvo = resolverAlvo;
        _resolverValor = resolverValor;
    }

    public string Id { get; }

    public Task<RegistroAlteracao> AplicarAsync(
        string acaoId,
        CategoriaAcao categoria,
        IReadOnlyDictionary<string, string> parametros,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        cancellationToken.ThrowIfCancellationRequested();

        var alvo = _resolverAlvo(parametros);
        var valorNovo = _resolverValor(parametros);
        var valorAnterior = _estado.Ler(alvo);

        _estado.Escrever(alvo, valorNovo);

        var registro = new RegistroAlteracao
        {
            AcaoId = acaoId,
            ComandoId = Id,
            Categoria = categoria,
            Alvo = alvo,
            ValorAnterior = valorAnterior,
            ValorNovo = valorNovo,
        };

        return Task.FromResult(registro);
    }

    public Task ReverterAsync(RegistroAlteracao registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        cancellationToken.ThrowIfCancellationRequested();

        _estado.Restaurar(registro.Alvo, registro.ValorAnterior);
        return Task.CompletedTask;
    }
}
