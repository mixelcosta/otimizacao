using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Comando interno determinístico e versionado. É a única coisa que de fato
/// altera o sistema; nunca é gerado pelo LLM. Aplica e reverte uma ação,
/// produzindo o registro auditável correspondente.
/// </summary>
public interface IComandoInterno
{
    /// <summary>Identificador versionado (ex.: "cmd.so.system_responsiveness.v1").</summary>
    string Id { get; }

    Task<RegistroAlteracao> AplicarAsync(
        string acaoId,
        CategoriaAcao categoria,
        IReadOnlyDictionary<string, string> parametros,
        CancellationToken cancellationToken = default);

    Task ReverterAsync(RegistroAlteracao registro, CancellationToken cancellationToken = default);
}
