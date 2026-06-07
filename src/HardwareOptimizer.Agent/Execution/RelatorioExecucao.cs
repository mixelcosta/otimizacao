using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>Desfecho da aplicação de uma categoria.</summary>
public enum SituacaoCategoria
{
    Aplicada = 0,
    Revertida = 1,
    Bloqueada = 2,
}

/// <summary>Resultado da aplicação de uma categoria, com suas alterações e validação.</summary>
public sealed record ResultadoCategoria
{
    public required CategoriaAcao Categoria { get; init; }

    public required SituacaoCategoria Situacao { get; init; }

    public IReadOnlyList<RegistroAlteracao> Alteracoes { get; init; } = Array.Empty<RegistroAlteracao>();

    public ResultadoValidacao? Validacao { get; init; }

    public IReadOnlyList<string> Mensagens { get; init; } = Array.Empty<string>();
}

/// <summary>Relatório consolidado da execução de um perfil.</summary>
public sealed record RelatorioExecucao
{
    public required bool Sucesso { get; init; }

    public required string PerfilNome { get; init; }

    public IReadOnlyList<ResultadoCategoria> Categorias { get; init; } = Array.Empty<ResultadoCategoria>();

    public IReadOnlyList<string> Mensagens { get; init; } = Array.Empty<string>();

    public IEnumerable<RegistroAlteracao> TodasAlteracoes =>
        Categorias.SelectMany(c => c.Alteracoes);
}
