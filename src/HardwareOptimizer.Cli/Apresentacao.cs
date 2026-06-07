using System.Text.Json;
using System.Text.Json.Serialization;

namespace HardwareOptimizer.Cli;

/// <summary>Helpers de saída no console para os fluxos da CLI.</summary>
internal static class Apresentacao
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Titulo(string texto)
    {
        Console.WriteLine();
        Console.WriteLine("== " + texto + " ==");
    }

    public static void Item(string rotulo, string? valor) =>
        Console.WriteLine($"  - {rotulo}: {valor ?? "(n/d)"}");

    public static void Linha(string texto = "") => Console.WriteLine(texto);

    public static void ImprimirJson<T>(T objeto) =>
        Console.WriteLine(JsonSerializer.Serialize(objeto, Json));
}
