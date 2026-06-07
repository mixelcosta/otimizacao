using System.Collections.Concurrent;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Abstração do estado mutável do sistema operacional (chaves de registro,
/// planos de energia, serviços). Permite ler o valor atual, escrever um novo e
/// restaurar o anterior — base do registro antes/depois e do rollback.
/// </summary>
public interface IEstadoSistema
{
    string? Ler(string alvo);

    void Escrever(string alvo, string valor);

    /// <summary>Restaura o valor anterior. Nulo significa "remover/voltar ao não definido".</summary>
    void Restaurar(string alvo, string? valorAnterior);
}

/// <summary>
/// Estado simulado em memória — modo seguro (dry-run) padrão do MVP. Não toca o
/// sistema real, mas reproduz fielmente a semântica de ler/escrever/restaurar,
/// tornando o executor e o rollback totalmente testáveis. Implementações reais
/// (powercfg, registro, sc.exe) substituem esta em Windows elevado.
/// </summary>
public sealed class EstadoSistemaSimulado : IEstadoSistema
{
    private readonly ConcurrentDictionary<string, string> _valores;

    public EstadoSistemaSimulado(IReadOnlyDictionary<string, string>? estadoInicial = null)
    {
        _valores = estadoInicial is null
            ? new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new ConcurrentDictionary<string, string>(estadoInicial, StringComparer.OrdinalIgnoreCase);
    }

    public string? Ler(string alvo) => _valores.TryGetValue(alvo, out var valor) ? valor : null;

    public void Escrever(string alvo, string valor) => _valores[alvo] = valor;

    public void Restaurar(string alvo, string? valorAnterior)
    {
        if (valorAnterior is null)
        {
            _valores.TryRemove(alvo, out _);
        }
        else
        {
            _valores[alvo] = valorAnterior;
        }
    }
}
