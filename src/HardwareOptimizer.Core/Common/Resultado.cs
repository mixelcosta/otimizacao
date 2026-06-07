namespace HardwareOptimizer.Core.Common;

/// <summary>
/// Resultado de uma operação que pode falhar, sem recorrer a exceções para o
/// fluxo de validação. Mantém a lista de erros legível para a UI e a auditoria.
/// </summary>
public sealed class Resultado
{
    private Resultado(bool sucesso, IReadOnlyList<string> erros)
    {
        Sucesso = sucesso;
        Erros = erros;
    }

    public bool Sucesso { get; }

    public bool Falha => !Sucesso;

    public IReadOnlyList<string> Erros { get; }

    public string MensagemErro => string.Join(" | ", Erros);

    public static Resultado Ok() => new(true, Array.Empty<string>());

    public static Resultado Falhar(params string[] erros) =>
        new(false, erros.Length == 0 ? new[] { "Falha não especificada." } : erros);

    public static Resultado Falhar(IReadOnlyList<string> erros) => new(false, erros);
}

/// <summary>Variante de <see cref="Resultado"/> que carrega um valor em caso de sucesso.</summary>
public sealed class Resultado<T>
{
    private Resultado(bool sucesso, T? valor, IReadOnlyList<string> erros)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erros = erros;
    }

    public bool Sucesso { get; }

    public bool Falha => !Sucesso;

    public T? Valor { get; }

    public IReadOnlyList<string> Erros { get; }

    public string MensagemErro => string.Join(" | ", Erros);

    public T ValorObrigatorio => Sucesso && Valor is not null
        ? Valor
        : throw new InvalidOperationException("Resultado sem valor: " + MensagemErro);

    public static Resultado<T> Ok(T valor) => new(true, valor, Array.Empty<string>());

    public static Resultado<T> Falhar(params string[] erros) =>
        new(false, default, erros.Length == 0 ? new[] { "Falha não especificada." } : erros);

    public static Resultado<T> Falhar(IReadOnlyList<string> erros) => new(false, default, erros);
}
