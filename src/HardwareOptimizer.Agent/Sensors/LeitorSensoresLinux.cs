using System.Globalization;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores para Linux. Lê os pseudo-arquivos de /sys/class/hwmon
/// (temperatura, fan, voltagem, consumo) e a frequência atual da CPU em
/// /sys/devices/system/cpu. Os caminhos-base são injetáveis para teste.
/// </summary>
public sealed class LeitorSensoresLinux : ILeitorSensores
{
    private readonly string _baseHwmon;
    private readonly string _baseCpu;
    private readonly ILogger _log;

    public LeitorSensoresLinux(string? baseHwmon = null, string? baseCpu = null, ILogger? logger = null)
    {
        _baseHwmon = baseHwmon ?? "/sys/class/hwmon";
        _baseCpu = baseCpu ?? "/sys/devices/system/cpu";
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Linux;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sensores = new List<Sensor>();
        LerHwmon(sensores);
        LerClockCpu(sensores);

        if (sensores.Count == 0)
        {
            _log.LogWarning("Nenhum sensor legível em '{Base}' (driver/permissão ausente?).", _baseHwmon);
        }

        return Task.FromResult(new LeituraSensores { Sensores = sensores });
    }

    private void LerHwmon(List<Sensor> sensores)
    {
        foreach (var dir in EnumerarDiretorios(_baseHwmon))
        {
            var chip = LerTexto(Path.Combine(dir, "name")) ?? Path.GetFileName(dir);

            foreach (var arquivo in EnumerarArquivos(dir))
            {
                var nome = Path.GetFileName(arquivo);
                if (!nome.EndsWith("_input", StringComparison.Ordinal))
                {
                    continue;
                }

                var chave = nome[..^"_input".Length];
                var (prefixo, indice) = SepararPrefixoIndice(chave);
                var mapeamento = Mapear(prefixo);
                if (mapeamento is null)
                {
                    continue;
                }

                var bruto = LerNumero(arquivo);
                if (bruto is not { } valorBruto)
                {
                    continue;
                }

                var (tipo, unidade, fator) = mapeamento.Value;
                var rotulo = LerTexto(Path.Combine(dir, $"{prefixo}{indice}_label"));

                sensores.Add(new Sensor
                {
                    Nome = rotulo ?? $"{chip} {prefixo}{indice}",
                    Tipo = tipo,
                    Valor = Math.Round(valorBruto * fator, 2),
                    Unidade = unidade,
                });
            }
        }
    }

    private void LerClockCpu(List<Sensor> sensores)
    {
        var raizCpu = Path.Combine(_baseCpu);
        double? maiorKHz = null;
        foreach (var dir in EnumerarDiretorios(raizCpu))
        {
            var nome = Path.GetFileName(dir);
            if (!nome.StartsWith("cpu", StringComparison.Ordinal)
                || !nome[3..].All(char.IsDigit) || nome.Length == 3)
            {
                continue;
            }

            var freq = LerNumero(Path.Combine(dir, "cpufreq", "scaling_cur_freq"));
            if (freq is { } khz && (maiorKHz is null || khz > maiorKHz))
            {
                maiorKHz = khz;
            }
        }

        if (maiorKHz is { } maior)
        {
            sensores.Add(new Sensor
            {
                Nome = "CPU (clock atual máx.)",
                Tipo = TipoSensor.Clock,
                Valor = Math.Round(maior / 1000.0, 0),
                Unidade = "MHz",
            });
        }
    }

    private static (TipoSensor Tipo, string Unidade, double Fator)? Mapear(string prefixo) => prefixo switch
    {
        "temp" => (TipoSensor.Temperatura, "°C", 0.001),
        "fan" => (TipoSensor.Fan, "RPM", 1.0),
        "in" => (TipoSensor.Voltagem, "V", 0.001),
        "power" => (TipoSensor.Potencia, "W", 0.000001),
        _ => null,
    };

    private static (string Prefixo, string Indice) SepararPrefixoIndice(string chave)
    {
        var i = 0;
        while (i < chave.Length && !char.IsDigit(chave[i]))
        {
            i++;
        }

        return (chave[..i], chave[i..]);
    }

    private double? LerNumero(string caminho)
    {
        var texto = LerTexto(caminho);
        return texto is not null
            && double.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private string? LerTexto(string caminho)
    {
        try
        {
            return File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerarDiretorios(string caminho)
    {
        try
        {
            return Directory.Exists(caminho) ? Directory.EnumerateDirectories(caminho) : Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerarArquivos(string caminho)
    {
        try
        {
            return Directory.EnumerateFiles(caminho);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
