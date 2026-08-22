using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Bios;

/// <summary>Ganho estimado de uma atualização de BIOS.</summary>
public enum GanhoEstimado
{
    Nenhum = 0,
    Baixo = 1,
    Medio = 2,
    Alto = 3,
}

/// <summary>Dados identificados da BIOS atual (passo "Identificação").</summary>
public sealed record IdentificacaoBios
{
    public required string FabricanteBruto { get; init; }

    /// <summary>Fabricante normalizado (ex.: "ASUS").</summary>
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public string? VersaoAtual { get; init; }

    public string? Data { get; init; }

    public string? Modo { get; init; }

    public bool? SecureBoot { get; init; }

    public required string ChaveBusca { get; init; }

    public static IdentificacaoBios DeInventario(Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        return DeInventario(inventario.Placa);
    }

    /// <summary>
    /// Overload que recebe só <see cref="PlacaMae"/> (sem o <see cref="Inventario"/>
    /// inteiro) — necessário para consumidores que só têm a placa já coletada em
    /// mãos, como o handler IPC "verificarbios" (spec-1-4).
    /// </summary>
    public static IdentificacaoBios DeInventario(PlacaMae placa)
    {
        ArgumentNullException.ThrowIfNull(placa);

        return new IdentificacaoBios
        {
            FabricanteBruto = placa.Fabricante,
            Fabricante = NormalizadorFabricante.Normalizar(placa.Fabricante),
            Modelo = placa.Modelo,
            VersaoAtual = placa.VersaoBios,
            Data = placa.DataBios,
            Modo = placa.Modo,
            SecureBoot = placa.SecureBoot,
            ChaveBusca = NormalizadorFabricante.GerarChaveBusca(placa.Fabricante, placa.Modelo),
        };
    }
}

/// <summary>Informação obtida do fabricante (passo "Verificação com fabricante").</summary>
public sealed record InfoBiosFabricante
{
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public required string VersaoMaisRecente { get; init; }

    public string? DataMaisRecente { get; init; }

    public string? Changelog { get; init; }

    public string? LinkManual { get; init; }

    /// <summary>Fonte sempre visível (exigência do documento).</summary>
    public required string Fonte { get; init; }

    public GanhoEstimado Ganho { get; init; }

    public string? Motivo { get; init; }
}

/// <summary>Decisão conservadora sobre atualizar ou não (passo "Decisão conservadora").</summary>
public sealed record DecisaoBios
{
    public required bool RecomendaAtualizar { get; init; }

    public required GanhoEstimado Ganho { get; init; }

    public required NivelRisco Risco { get; init; }

    public required string Justificativa { get; init; }

    public string? Fonte { get; init; }

    public string? VersaoAtual { get; init; }

    public string? VersaoRecomendada { get; init; }
}

/// <summary>Guia passo a passo específico do fabricante (passo "Guia passo a passo").</summary>
public sealed record GuiaBios
{
    public required string TeclaSetup { get; init; }

    public required string Utilitario { get; init; }

    public IReadOnlyList<string> Passos { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AjustesRecomendados { get; init; } = Array.Empty<string>();
}

/// <summary>Relatório consolidado do módulo BIOS, ponta a ponta.</summary>
public sealed record RelatorioBios
{
    public required IdentificacaoBios Identificacao { get; init; }

    public InfoBiosFabricante? InfoFabricante { get; init; }

    public required DecisaoBios Decisao { get; init; }

    public required GuiaBios Guia { get; init; }

    /// <summary>Houve correspondência no fabricante/banco curado?</summary>
    public bool FonteEncontrada => InfoFabricante is not null;
}
