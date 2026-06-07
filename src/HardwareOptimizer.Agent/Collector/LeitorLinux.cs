using System.Globalization;
using System.Runtime.InteropServices;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário para Linux. Lê exclusivamente pseudo-arquivos do sistema
/// (/sys, /proc), sem invocar binários nem modificar nada. Campos indisponíveis
/// (ex.: que exigem root) são deixados nulos, sem falhar a coleta.
/// </summary>
public sealed class LeitorLinux : ILeitorPlataforma
{
    private const string DmiBase = "/sys/class/dmi/id";

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Linux;

    public Task<Inventario> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inventario = new Inventario
        {
            Placa = LerPlaca(),
            Cpu = LerCpu(),
            Memoria = LerMemoria(),
            Gpu = Array.Empty<PlacaVideo>(), // Nome legível de GPU exige lspci; fora do MVP Linux.
            SistemaOperacional = LerSistemaOperacional(),
            Rede = LerRede(),
            Identificadores = LerIdentificadores(),
            ColetadoEm = DateTimeOffset.UtcNow,
        };

        return Task.FromResult(inventario);
    }

    private static PlacaMae LerPlaca() => new()
    {
        Fabricante = LerTexto($"{DmiBase}/board_vendor") ?? "Desconhecido",
        Modelo = LerTexto($"{DmiBase}/board_name") ?? "Desconhecido",
        VersaoBios = LerTexto($"{DmiBase}/bios_version"),
        DataBios = LerTexto($"{DmiBase}/bios_date"),
        Modo = Directory.Exists("/sys/firmware/efi") ? "UEFI" : "Legacy",
        SecureBoot = LerSecureBoot(),
    };

    private static Processador LerCpu()
    {
        var nome = "Desconhecido";
        var threads = 0;
        var nucleos = new HashSet<string>(StringComparer.Ordinal);
        string? physicalId = null;

        foreach (var linha in LerLinhas("/proc/cpuinfo"))
        {
            var (chave, valor) = SepararChaveValor(linha);
            switch (chave)
            {
                case "model name":
                    nome = valor;
                    break;
                case "processor":
                    threads++;
                    break;
                case "physical id":
                    physicalId = valor;
                    break;
                case "core id":
                    nucleos.Add($"{physicalId}:{valor}");
                    break;
            }
        }

        return new Processador
        {
            Nome = nome,
            Threads = threads > 0 ? threads : null,
            Nucleos = nucleos.Count > 0 ? nucleos.Count : null,
            TempIdleC = LerTemperaturaCpu(),
        };
    }

    private static IReadOnlyList<ModuloMemoria> LerMemoria()
    {
        foreach (var linha in LerLinhas("/proc/meminfo"))
        {
            var (chave, valor) = SepararChaveValor(linha);
            if (chave != "MemTotal")
            {
                continue;
            }

            // Valor no formato "16384000 kB".
            var numero = valor.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (long.TryParse(numero, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            {
                var gb = (int)Math.Round(kb / 1024.0 / 1024.0);
                return new[] { new ModuloMemoria { TamanhoGb = gb } };
            }
        }

        return Array.Empty<ModuloMemoria>();
    }

    private static SistemaOperacionalInfo LerSistemaOperacional() => new()
    {
        Tipo = SistemaOperacionalTipo.Linux,
        Nome = LerOsReleasePrettyName() ?? "Linux",
        Versao = Environment.OSVersion.VersionString,
        Arquitetura = RuntimeInformation.OSArchitecture.ToString(),
    };

    private static IReadOnlyList<InterfaceRede> LerRede()
    {
        const string baseRede = "/sys/class/net";
        if (!Directory.Exists(baseRede))
        {
            return Array.Empty<InterfaceRede>();
        }

        var interfaces = new List<InterfaceRede>();
        foreach (var dir in EnumerarDiretorios(baseRede))
        {
            var nome = Path.GetFileName(dir);
            if (nome == "lo")
            {
                continue;
            }

            interfaces.Add(new InterfaceRede
            {
                Nome = nome,
                EnderecoMac = LerTexto(Path.Combine(dir, "address")),
            });
        }

        return interfaces;
    }

    private static IdentificadoresSensiveis LerIdentificadores() => new()
    {
        NumeroSerie = LerTexto($"{DmiBase}/product_serial"),
        UuidPlaca = LerTexto($"{DmiBase}/product_uuid"),
        NomeMaquina = SeguroOuNulo(() => Environment.MachineName),
        NomeUsuario = SeguroOuNulo(() => Environment.UserName),
        ChaveProdutoWindows = null,
    };

    private static bool? LerSecureBoot()
    {
        // A variável EFI SecureBoot tem um cabeçalho de 4 bytes seguido do valor.
        var arquivos = SeguroOuNulo(() => Directory.Exists("/sys/firmware/efi/efivars")
            ? Directory.GetFiles("/sys/firmware/efi/efivars", "SecureBoot-*")
            : Array.Empty<string>());

        if (arquivos is null || arquivos.Length == 0)
        {
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(arquivos[0]);
            return bytes.Length >= 5 ? bytes[4] == 1 : null;
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

    private static double? LerTemperaturaCpu()
    {
        var texto = LerTexto("/sys/class/thermal/thermal_zone0/temp");
        if (texto is not null &&
            long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mili))
        {
            return Math.Round(mili / 1000.0, 1);
        }

        return null;
    }

    private static string? LerOsReleasePrettyName()
    {
        foreach (var linha in LerLinhas("/etc/os-release"))
        {
            if (linha.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
            {
                return linha["PRETTY_NAME=".Length..].Trim('"');
            }
        }

        return null;
    }

    private static (string Chave, string Valor) SepararChaveValor(string linha)
    {
        var idx = linha.IndexOf(':', StringComparison.Ordinal);
        return idx < 0
            ? (linha.Trim(), string.Empty)
            : (linha[..idx].Trim(), linha[(idx + 1)..].Trim());
    }

    private static string? LerTexto(string caminho)
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

    private static IEnumerable<string> LerLinhas(string caminho)
    {
        string[]? linhas = null;
        try
        {
            if (File.Exists(caminho))
            {
                linhas = File.ReadAllLines(caminho);
            }
        }
        catch (IOException)
        {
            // Ignorado: arquivo indisponível resulta em coleta parcial.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorado: sem permissão resulta em coleta parcial.
        }

        return linhas ?? Array.Empty<string>();
    }

    private static IEnumerable<string> EnumerarDiretorios(string caminho)
    {
        try
        {
            return Directory.EnumerateDirectories(caminho);
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

    private static T? SeguroOuNulo<T>(Func<T> acao)
        where T : class
    {
        try
        {
            return acao();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
