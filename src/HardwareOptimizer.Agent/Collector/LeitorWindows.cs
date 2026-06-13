using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário para Windows (plataforma prioritária do MVP). Usa
/// PowerShell + CIM/WMI em modo somente leitura (Get-CimInstance). É defensivo:
/// qualquer falha de uma consulta resulta em coleta parcial, nunca em exceção.
/// A validação real ocorre em máquinas Windows (Fase 1 do roadmap).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorWindows : ILeitorPlataforma
{
    private readonly ILogger _log;

    public LeitorWindows(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<Inventario> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo inventário do Windows via PowerShell/CIM (Get-CimInstance).");

        var inventario = new Inventario
        {
            Placa = LerPlaca(),
            Cpu = LerCpu(),
            Memoria = LerMemoria(),
            Gpu = LerGpu(),
            SistemaOperacional = LerSistemaOperacional(),
            Rede = LerRede(),
            Identificadores = LerIdentificadores(),
            ColetadoEm = DateTimeOffset.UtcNow,
        };

        if (inventario.Placa.Fabricante == "Desconhecido")
        {
            _log.LogWarning(
                "Coleta Windows parcial: consultas CIM/PowerShell não retornaram dados "
                + "(PowerShell ausente, sem permissão ou execução bloqueada).");
        }

        return Task.FromResult(inventario);
    }

    private static PlacaMae LerPlaca()
    {
        var board = PrimeiroItem("Win32_BaseBoard", "Manufacturer,Product,SerialNumber");
        var bios = PrimeiroItem("Win32_BIOS", "SMBIOSBIOSVersion,ReleaseDate");

        return new PlacaMae
        {
            Fabricante = Texto(board, "Manufacturer") ?? "Desconhecido",
            Modelo = Texto(board, "Product") ?? "Desconhecido",
            VersaoBios = Texto(bios, "SMBIOSBIOSVersion"),
            DataBios = NormalizadorData.Normalizar(Texto(bios, "ReleaseDate")),
            Modo = LerTexto("$env:firmware_type") is { Length: > 0 } modo ? modo : null,
            SecureBoot = LerSecureBoot(),
        };
    }

    private static Processador LerCpu()
    {
        var cpu = PrimeiroItem("Win32_Processor", "Name,NumberOfCores,NumberOfLogicalProcessors");
        return new Processador
        {
            Nome = Texto(cpu, "Name") ?? "Desconhecido",
            Nucleos = Inteiro(cpu, "NumberOfCores"),
            Threads = Inteiro(cpu, "NumberOfLogicalProcessors"),
        };
    }

    private static IReadOnlyList<ModuloMemoria> LerMemoria()
    {
        var modulos = new List<ModuloMemoria>();
        foreach (var item in Itens("Win32_PhysicalMemory",
            "Capacity,Speed,Manufacturer,PartNumber,DeviceLocator,SMBIOSMemoryType"))
        {
            int? gb = null;
            var capacidade = NumeroLong(item, "Capacity");
            if (capacidade.HasValue && capacidade.Value > 0)
                gb = (int)Math.Round(capacidade.Value / 1024.0 / 1024.0 / 1024.0);

            modulos.Add(new ModuloMemoria
            {
                TamanhoGb = gb,
                VelocidadeMhz = Inteiro(item, "Speed"),
                Fabricante = DecodificarFabricanteRam(Texto(item, "Manufacturer")),
                Modelo = Texto(item, "PartNumber"),
                Slot = Texto(item, "DeviceLocator"),
                Tipo = DecodificarTipoMemoria(Inteiro(item, "SMBIOSMemoryType")),
            });
        }

        return modulos;
    }

    private static string? DecodificarFabricanteRam(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trim = raw.Trim();
        // Se já é nome legível (contém letra não-hex), devolve direto
        if (trim.Any(c => c is > 'F' and <= 'Z' or > 'f' and <= 'z' or ' '))
            return trim;
        return trim.ToUpperInvariant().Replace(" ", "") switch
        {
            "CE"     or "04CE" or "80CE"     => "Samsung",
            "AD"     or "80AD" or "04AD"     => "SK Hynix",
            "2C"     or "802C" or "04F1"     => "Micron",
            "9E"     or "029E"               => "Corsair",
            "61"     or "04CB" or "04CD"
                    or "0198"                => "Kingston",
            "5105"                           => "Qimonda",
            "C1"     or "04C1"               => "Infineon",
            "51"     or "0151"               => "Qimonda",
            "98"     or "0198"               => "Crucial",
            _ => trim,
        };
    }

    private static string? DecodificarTipoMemoria(int? tipo) => tipo switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _  => null,
    };

    private static IReadOnlyList<PlacaVideo> LerGpu()
    {
        var gpus = new List<PlacaVideo>();
        foreach (var item in Itens("Win32_VideoController", "Name,DriverVersion"))
        {
            var nome = Texto(item, "Name");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                gpus.Add(new PlacaVideo { Nome = nome, VersaoDriver = Texto(item, "DriverVersion") });
            }
        }

        return gpus;
    }

    private static SistemaOperacionalInfo LerSistemaOperacional()
    {
        var os = PrimeiroItem("Win32_OperatingSystem", "Caption,Version");
        return new SistemaOperacionalInfo
        {
            Tipo = SistemaOperacionalTipo.Windows,
            Nome = Texto(os, "Caption") ?? "Windows",
            Versao = Texto(os, "Version") ?? Environment.OSVersion.VersionString,
            Arquitetura = RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static IReadOnlyList<InterfaceRede> LerRede()
    {
        var interfaces = new List<InterfaceRede>();
        foreach (var item in Itens(
            "Win32_NetworkAdapter -Filter 'PhysicalAdapter=True'", "NetConnectionID,MACAddress"))
        {
            var nome = Texto(item, "NetConnectionID");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                interfaces.Add(new InterfaceRede { Nome = nome, EnderecoMac = Texto(item, "MACAddress") });
            }
        }

        return interfaces;
    }

    private static IdentificadoresSensiveis LerIdentificadores()
    {
        var board = PrimeiroItem("Win32_BaseBoard", "SerialNumber");
        var produto = PrimeiroItem("Win32_ComputerSystemProduct", "UUID");
        return new IdentificadoresSensiveis
        {
            NumeroSerie = Texto(board, "SerialNumber"),
            UuidPlaca = Texto(produto, "UUID"),
            NomeMaquina = SeguroOuNulo(() => Environment.MachineName),
            NomeUsuario = SeguroOuNulo(() => Environment.UserName),
            ChaveProdutoWindows = null, // exige leitura adicional; sensível, omitido por padrão.
        };
    }

    private static bool? LerSecureBoot()
    {
        var saida = LerTexto("try { Confirm-SecureBootUEFI } catch { '' }");
        return bool.TryParse(saida, out var valor) ? valor : null;
    }

    // ---- Infraestrutura CIM/PowerShell ----------------------------------------------------

    private static JsonElement? PrimeiroItem(string classe, string propriedades) =>
        Itens(classe, propriedades).FirstOrDefault() is { ValueKind: not JsonValueKind.Undefined } e ? e : null;

    private static IEnumerable<JsonElement> Itens(string classe, string propriedades)
    {
        var saida = LerTexto(
            $"Get-CimInstance -ClassName {classe} | Select-Object {propriedades} | ConvertTo-Json -Compress -Depth 3");
        if (string.IsNullOrWhiteSpace(saida))
        {
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(saida);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    yield return item.Clone();
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                yield return doc.RootElement.Clone();
            }
        }
    }

    private static string? Texto(JsonElement? elemento, string propriedade)
    {
        if (elemento is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var texto = prop.GetString();
            return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        }

        return null;
    }

    private static int? Inteiro(JsonElement? elemento, string propriedade)
    {
        if (elemento is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var prop) &&
            prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var valor))
        {
            return valor;
        }

        return null;
    }

    private static long? NumeroLong(JsonElement? elemento, string propriedade)
    {
        if (elemento is not { } e || e.ValueKind != JsonValueKind.Object) return null;
        if (!e.TryGetProperty(propriedade, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var v)) return v;
        if (prop.ValueKind == JsonValueKind.String &&
            long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            return s;
        return null;
    }

    private static string? LerTexto(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{comando}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(psi);
            if (processo is null)
            {
                return null;
            }

            var saida = processo.StandardOutput.ReadToEnd();
            if (!processo.WaitForExit(20_000))
            {
                return null;
            }

            return saida.Trim();
        }
        catch (Win32Exception)
        {
            return null; // PowerShell ausente.
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
    }
}
