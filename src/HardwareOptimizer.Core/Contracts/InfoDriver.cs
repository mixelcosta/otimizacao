namespace HardwareOptimizer.Core.Contracts;

public enum StatusDriver
{
    Atualizado = 0,
    AtualizacaoDisponivel = 1,
    Desconhecido = 2,
}

public sealed record InfoDriver
{
    public required string HardwareId { get; init; }

    public required string Descricao { get; init; }

    public string? Fabricante { get; init; }

    public string? VersaoAtual { get; init; }

    public string? VersaoDisponivel { get; init; }

    public string? UrlDownload { get; init; }

    public bool CertificadoWhql { get; init; }

    public StatusDriver Status { get; init; }
}
