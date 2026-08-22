namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Só existem os dois status "com dado real" — ao contrário de
/// <see cref="StatusDriver"/>, não há um valor "Desconhecido": quando a fonte
/// oficial não tem cobertura para um programa, o item simplesmente não aparece
/// na lista devolvida por <c>VerificadorSoftware</c> (guard anti-alucinação).
/// </summary>
public enum StatusSoftware
{
    Atualizado = 0,
    AtualizacaoDisponivel = 1,
}

public sealed record InfoSoftware
{
    public required string Nome { get; init; }

    public string? VersaoAtual { get; init; }

    public string? VersaoDisponivel { get; init; }

    public string? UrlDownload { get; init; }

    public StatusSoftware Status { get; init; }
}
