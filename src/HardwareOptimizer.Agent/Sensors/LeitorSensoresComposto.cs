using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Encadeia leitores de sensores em paralelo e mescla os resultados, permitindo
/// degradação graciosa: LHM (rico), ADL (GPU AMD sem admin) e WMI (fallback)
/// rodam simultaneamente — cada um com seu próprio lock interno.
/// </summary>
public sealed class LeitorSensoresComposto : ILeitorSensores
{
    private readonly IReadOnlyList<ILeitorSensores> _leitores;
    private readonly ILogger _log;

    public LeitorSensoresComposto(IReadOnlyList<ILeitorSensores> leitores, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(leitores);
        if (leitores.Count == 0)
            throw new ArgumentException("Informe ao menos um leitor.", nameof(leitores));

        _leitores = leitores;
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => _leitores[0].Tipo;

    public async Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        // Dispara todos os leitores em paralelo — cada um tem seu próprio SemaphoreSlim interno,
        // portanto são thread-safe. Deduplicação por nome: o primeiro leitor que emite ganha.
        var tarefas = _leitores.Select(l => l.LerAsync(cancellationToken)).ToArray();
        var resultados = await Task.WhenAll(tarefas).ConfigureAwait(false);

        var todos = new List<Sensor>();
        var nomesVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var leitura in resultados)
        {
            _log.LogDebug("Leitor: {Qtd} sensor(es).", leitura.Sensores.Count);
            foreach (var s in leitura.Sensores)
            {
                if (nomesVistos.Add(s.Nome))
                    todos.Add(s);
            }
        }

        if (todos.Count == 0)
            _log.LogWarning("Nenhum leitor de sensores retornou dados.");

        return new LeituraSensores { Sensores = todos };
    }
}
