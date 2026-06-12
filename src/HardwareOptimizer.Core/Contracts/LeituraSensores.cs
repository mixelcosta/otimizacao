namespace HardwareOptimizer.Core.Contracts;

/// <summary>Grandeza de um sensor de hardware.</summary>
public enum TipoSensor
{
    Temperatura = 0,
    Clock = 1,
    Voltagem = 2,
    Fan = 3,
    Potencia = 4,
    Outro = 5,
    Carga = 6,
}

/// <summary>Leitura de um único sensor.</summary>
public sealed record Sensor
{
    public required string Nome { get; init; }

    public required TipoSensor Tipo { get; init; }

    public required double Valor { get; init; }

    public required string Unidade { get; init; }
}

/// <summary>
/// Leitura instantânea dos sensores (temperatura, clock, voltagem, rotação de
/// fan e consumo), em tempo real. Saída do módulo de sensores.
/// </summary>
public sealed record LeituraSensores
{
    public DateTimeOffset Momento { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<Sensor> Sensores { get; init; } = Array.Empty<Sensor>();

    public IEnumerable<Sensor> PorTipo(TipoSensor tipo) => Sensores.Where(s => s.Tipo == tipo);

    /// <summary>Maior temperatura lida (°C), ou nulo se não houver sensor de temperatura.</summary>
    public double? TemperaturaMaxC
    {
        get
        {
            var temperaturas = PorTipo(TipoSensor.Temperatura).Select(s => s.Valor).ToList();
            return temperaturas.Count == 0 ? null : temperaturas.Max();
        }
    }
}
