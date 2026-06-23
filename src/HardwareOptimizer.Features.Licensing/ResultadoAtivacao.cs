namespace HardwareOptimizer.Features.Licensing;

public sealed record ResultadoAtivacao
{
    public required bool Sucesso { get; init; }
    public TipoLicenca? NovoTipo { get; init; }
    public string? Erro { get; init; }
    public string? NomeCliente { get; init; }
    public string? EmailCliente { get; init; }

    public static ResultadoAtivacao Ok(TipoLicenca tipo, string? nome = null, string? email = null) =>
        new() { Sucesso = true, NovoTipo = tipo, NomeCliente = nome, EmailCliente = email };

    public static ResultadoAtivacao Falhar(string erro) =>
        new() { Sucesso = false, Erro = erro };
}
