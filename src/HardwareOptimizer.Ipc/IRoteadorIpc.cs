namespace HardwareOptimizer.Ipc;

/// <summary>
/// Abstração do roteador do agente. Permite que a UI (e os testes) dependam do
/// contrato em vez da implementação concreta, e que a UI fale com o agente em
/// processo (<see cref="RoteadorIpc"/>) ou remoto (via named pipe) de forma
/// intercambiável.
/// </summary>
public interface IRoteadorIpc
{
    Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default);
}
