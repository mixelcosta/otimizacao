using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Inventário normalizado do equipamento (contrato "inventario").
/// É a "impressão digital" da máquina: campos sensíveis ficam agrupados em
/// <see cref="Identificadores"/> para que a camada de privacidade os trate
/// antes de qualquer envio ao cérebro na nuvem.
/// </summary>
public sealed record Inventario
{
    public required PlacaMae Placa { get; init; }

    public required Processador Cpu { get; init; }

    public IReadOnlyList<ModuloMemoria> Memoria { get; init; } = Array.Empty<ModuloMemoria>();

    public IReadOnlyList<PlacaVideo> Gpu { get; init; } = Array.Empty<PlacaVideo>();

    public required SistemaOperacionalInfo SistemaOperacional { get; init; }

    public IReadOnlyList<InterfaceRede> Rede { get; init; } = Array.Empty<InterfaceRede>();

    /// <summary>Identificadores sensíveis. Nulo após a sanitização.</summary>
    public IdentificadoresSensiveis? Identificadores { get; init; }

    public DateTimeOffset ColetadoEm { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PlacaMae
{
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public string? VersaoBios { get; init; }

    public string? DataBios { get; init; }

    /// <summary>UEFI ou Legacy.</summary>
    public string? Modo { get; init; }

    public bool? SecureBoot { get; init; }

    /// <summary>Ex: "AMD X570" ou "Intel Z790".</summary>
    public string? Chipset { get; init; }

    /// <summary>Ex: "PCI-Express 4.0 (16.0 GT/s)".</summary>
    public string? BusSpecs { get; init; }
}

public sealed record Processador
{
    public required string Nome { get; init; }

    public int? Nucleos { get; init; }

    public int? Threads { get; init; }

    public double? TempIdleC { get; init; }

    /// <summary>Ex: "AMD" | "Intel".</summary>
    public string? Fabricante { get; init; }

    /// <summary>Ex: "AM4", "AM5", "LGA1700".</summary>
    public string? Soquete { get; init; }

    /// <summary>Clock base em MHz (MaxClockSpeed do WMI).</summary>
    public int? ClockBaseMhz { get; init; }

    /// <summary>Clock atual em MHz (CurrentClockSpeed do WMI).</summary>
    public int? ClockAtualMhz { get; init; }

    /// <summary>Cache L2 em KB.</summary>
    public int? L2CacheKb { get; init; }

    /// <summary>Cache L3 em KB.</summary>
    public int? L3CacheKb { get; init; }
}

public sealed record ModuloMemoria
{
    public int? TamanhoGb { get; init; }

    public int? VelocidadeMhz { get; init; }

    public string? Fabricante { get; init; }

    /// <summary>Número de parte (PartNumber) — ex: "CMK16GX4M2B3200C16".</summary>
    public string? Modelo { get; init; }

    /// <summary>Slot físico — ex: "DIMM A1".</summary>
    public string? Slot { get; init; }

    /// <summary>Tipo de memória decodificado — ex: "DDR4", "DDR5".</summary>
    public string? Tipo { get; init; }
}

public sealed record PlacaVideo
{
    public required string Nome { get; init; }

    public double? TempIdleC { get; init; }

    public string? VersaoDriver { get; init; }

    /// <summary>Ex: "x16".</summary>
    public string? LinkWidthAtual { get; init; }

    /// <summary>Ex: "x16".</summary>
    public string? LinkWidthMax { get; init; }

    /// <summary>Ex: "16.0 GT/s".</summary>
    public string? LinkSpeedAtual { get; init; }

    /// <summary>Ex: "16.0 GT/s".</summary>
    public string? LinkSpeedMax { get; init; }
}

public sealed record SistemaOperacionalInfo
{
    public required SistemaOperacionalTipo Tipo { get; init; }

    public string? Nome { get; init; }

    public string? Versao { get; init; }

    public string? Arquitetura { get; init; }
}

public sealed record InterfaceRede
{
    public required string Nome { get; init; }

    public string? Tipo { get; init; }

    /// <summary>Endereço MAC: sensível. Nulo/hasheado após a sanitização.</summary>
    public string? EnderecoMac { get; init; }
}

/// <summary>
/// Campos que identificam unicamente o equipamento ou o usuário.
/// Correspondem a <c>campos_sensiveis</c> do documento.
/// </summary>
public sealed record IdentificadoresSensiveis
{
    public string? NumeroSerie { get; init; }

    public string? UuidPlaca { get; init; }

    public string? NomeMaquina { get; init; }

    public string? NomeUsuario { get; init; }

    public string? ChaveProdutoWindows { get; init; }
}
