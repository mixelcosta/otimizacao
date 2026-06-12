namespace HardwareOptimizer.Features.Licensing;

public sealed record ResultadoAtivacao
{
    public required bool Sucesso { get; init; }

    public TipoLicenca? NovoTipo { get; init; }

    public string? Erro { get; init; }

    public static ResultadoAtivacao Ok(TipoLicenca tipo) =>
        new() { Sucesso = true, NovoTipo = tipo };

    public static ResultadoAtivacao Falhar(string erro) =>
        new() { Sucesso = false, Erro = erro };
}
