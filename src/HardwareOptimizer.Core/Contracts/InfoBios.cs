namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// BIOS desatualizada sinalizada por <c>VerificadorBios</c> (Features.Atualizacao,
/// spec-1-4), junto com o guia passo a passo de atualização já montado. Mesmo
/// estilo de <see cref="InfoSoftware"/>: só existe para itens já confirmados
/// como desatualizados — nunca é populado com "sem informação" genérico (guard
/// anti-alucinação).
/// </summary>
public sealed record InfoBios
{
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public string? VersaoAtual { get; init; }

    public string? VersaoDisponivel { get; init; }

    public string? UrlDownload { get; init; }

    public required string TeclaSetup { get; init; }

    public required string Utilitario { get; init; }

    public IReadOnlyList<string> Passos { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();
}
