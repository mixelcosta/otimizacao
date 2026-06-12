namespace HardwareOptimizer.Core.Contracts;

public enum ImpactoInicializacao
{
    Baixo = 0,
    Medio = 1,
    Alto = 2,
    Desconhecido = 3,
}

public enum OrigemInicializacao
{
    RegistroUsuario = 0,
    RegistroMaquina = 1,
    PastaStartup = 2,
}

public sealed record InicializacaoEntrada
{
    public required string Nome { get; init; }

    public required string Caminho { get; init; }

    public required ImpactoInicializacao Impacto { get; init; }

    public required OrigemInicializacao Origem { get; init; }

    public bool Ativo { get; init; }

    /// <summary>Chave de registro ou caminho de arquivo usada para desfazer a alteração.</summary>
    public string? ChaveRollback { get; init; }
}
