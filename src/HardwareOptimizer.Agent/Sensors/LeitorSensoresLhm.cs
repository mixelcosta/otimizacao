using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores baseado em LibreHardwareMonitor (clock, voltagem, fan,
/// consumo e temperatura por componente). Opera sobre a <see cref="IFonteSensoresLhm"/>,
/// o que mantém esta lógica (filtragem e empacotamento) testável em qualquer
/// plataforma; a fonte real só roda sob Windows com o driver carregado.
/// </summary>
public sealed class LeitorSensoresLhm : ILeitorSensores
{
    private readonly IFonteSensoresLhm _fonte;
    private readonly ILogger _log;
    // LHM não é thread-safe: semáforo garante acesso exclusivo; cache de 2s
    // evita chamadas simultâneas quando o timer de 500ms se adianta ao WMI.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private LeituraSensores? _cache;
    private DateTime _cacheExpira = DateTime.MinValue;
    private static readonly TimeSpan _intervaloCache = TimeSpan.FromSeconds(2);

    public LeitorSensoresLhm(IFonteSensoresLhm fonte, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        _fonte = fonte;
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache is not null && DateTime.UtcNow < _cacheExpira)
            return Task.FromResult(_cache);

        if (!_lock.Wait(0))
            return Task.FromResult(_cache ?? new LeituraSensores());

        if (_cache is not null && DateTime.UtcNow < _cacheExpira)
        {
            _lock.Release();
            return Task.FromResult(_cache);
        }

        try
        {
            _log.LogDebug("Lendo sensores via LibreHardwareMonitor.");

            // Descarta valores não finitos (NaN/Infinito) e leituras indisponíveis:
            // temperatura/clock em 0 indicam sensor não lido (típico da CPU sem o
            // driver/elevação — leituras de MSR exigem Ring0). Tensão, fan e potência
            // em 0 são válidos e permanecem.
            var validos = _fonte.Ler()
                .Where(s => double.IsFinite(s.Valor) && !LeituraIndisponivel(s))
                .ToList();

            if (validos.Count == 0)
                _log.LogWarning("LibreHardwareMonitor não retornou sensores (driver ausente ou sem elevação?).");
            else
                _log.LogDebug("LibreHardwareMonitor: {Qtd} sensor(es) válido(s).", validos.Count);

            _cache = new LeituraSensores { Sensores = validos };
            _cacheExpira = DateTime.UtcNow + _intervaloCache;
            return Task.FromResult(_cache);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Temperatura/clock ≤ 0 = sensor indisponível (não lido). Outros tipos podem
    // legitimamente valer 0 (ex.: fan parado, consumo ocioso).
    private static bool LeituraIndisponivel(Sensor sensor) =>
        sensor.Tipo is TipoSensor.Temperatura or TipoSensor.Clock && sensor.Valor <= 0;
}
