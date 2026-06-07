using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Registro auditável de uma alteração aplicada: guarda o alvo e os valores
/// anterior e novo, permitindo rollback determinístico por categoria.
/// </summary>
public sealed record RegistroAlteracao
{
    public required string AcaoId { get; init; }

    public required string ComandoId { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    /// <summary>Recurso afetado (ex.: chave de registro, plano de energia, serviço).</summary>
    public required string Alvo { get; init; }

    public string? ValorAnterior { get; init; }

    public string? ValorNovo { get; init; }

    public DateTimeOffset AplicadoEm { get; init; } = DateTimeOffset.UtcNow;

    public bool Revertido { get; init; }
}
