namespace HardwareOptimizer.Core.Contracts;

public enum StatusServico
{
    Rodando = 0,
    Parado = 1,
    Outro = 2,
}

public enum TipoInicioServico
{
    Automatico = 0,
    Manual = 1,
    Desabilitado = 2,
    Outro = 3,
}

public sealed record ServicoSistema
{
    public required string Nome { get; init; }

    public string? NomeExibicao { get; init; }

    public StatusServico Status { get; init; }

    public TipoInicioServico TipoInicio { get; init; }
}
