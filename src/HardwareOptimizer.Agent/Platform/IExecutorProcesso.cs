namespace HardwareOptimizer.Agent.Platform;

/// <summary>
/// Resultado da execução de um processo externo (powercfg, sc.exe).
/// </summary>
public sealed record ResultadoProcesso(int CodigoSaida, string SaidaPadrao, string SaidaErro)
{
    public bool Sucesso => CodigoSaida == 0;
}

/// <summary>
/// Porta para executar utilitários do sistema (powercfg, sc.exe). Síncrona para
/// casar com o contrato síncrono de <see cref="Execution.IEstadoSistema"/>;
/// abstraída para permitir fakes nos testes (sem tocar o sistema real).
/// </summary>
public interface IExecutorProcesso
{
    ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos);
}
