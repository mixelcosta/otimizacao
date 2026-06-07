using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Ferramenta de estresse que produz a saída textual a ser parseada. As
/// implementações reais invocam OCCT/Prime95/MemTest86; a simulada é usada no
/// MVP e nos testes.
/// </summary>
public interface IFerramentaEstresse
{
    string Nome { get; }

    Task<string> ExecutarAsync(CategoriaAcao categoria, TimeSpan duracao, CancellationToken cancellationToken = default);
}

/// <summary>Mapeia a categoria à ferramenta de estresse recomendada (do documento).</summary>
public static class SeletorFerramenta
{
    public static string Recomendada(CategoriaAcao categoria) => categoria switch
    {
        CategoriaAcao.Cpu => "OCCT/Prime95",
        CategoriaAcao.Memoria => "MemTest86/OCCT Memory",
        CategoriaAcao.Gpu => "OCCT GPU/VRAM",
        _ => "OCCT",
    };
}

/// <summary>
/// Ferramenta simulada: devolve uma saída pré-definida (sem invocar binários).
/// Os helpers geram saídas saudáveis ou com regressão para o MVP e os testes.
/// </summary>
public sealed class FerramentaEstresseSimulada : IFerramentaEstresse
{
    private readonly string _saida;

    public FerramentaEstresseSimulada(string nome, string saida)
    {
        Nome = nome;
        _saida = saida;
    }

    public string Nome { get; }

    public Task<string> ExecutarAsync(
        CategoriaAcao categoria, TimeSpan duracao, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_saida);
    }

    public static FerramentaEstresseSimulada Saudavel(string nome = "OCCT") =>
        new(nome,
            "Tool: " + nome + "\nWHEA errors: 0\nMemory errors: 0\nMax temperature: 78 C\n"
            + "Clock: 4600 MHz\nPower: 88 W\nScore: 11850\nArtifacts: no\nDriver timeout: no\n"
            + "BSOD: no\nStability: PASS");

    public static FerramentaEstresseSimulada ComRegressao(string motivo = "whea", string nome = "OCCT") =>
        new(nome, "Tool: " + nome + "\n" + motivo.Trim().ToLowerInvariant() switch
        {
            "bsod" => "WHEA errors: 0\nBSOD: yes\nStability: FAIL",
            "temp" => "WHEA errors: 0\nMax temperature: 99 C\nStability: WARN",
            "artefatos" => "WHEA errors: 0\nArtifacts: yes\nDriver timeout: yes\nStability: FAIL",
            "memoria" => "Memory errors: 7\nStability: FAIL",
            _ => "WHEA errors: 3\nMax temperature: 92 C\nStability: FAIL",
        });
}
