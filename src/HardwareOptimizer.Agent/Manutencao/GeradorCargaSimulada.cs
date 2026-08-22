using System.Diagnostics;

namespace HardwareOptimizer.Agent.Manutencao;

/// <summary>
/// Gerador de carga interno, curto e limitado em duração — produz uma janela de
/// carga real e comparável para o diagnóstico de manutenção (spec-2-1-deteccao-
/// pasta-termica-ressecada), sem depender de nenhuma ferramenta externa
/// (OCCT/Prime95/MemTest86). Deliberadamente não reaproveita
/// <c>IFerramentaEstresse</c> (Agent/Validation) — aquela infraestrutura
/// pressupõe uma ferramenta de terceiros disparada por minutos, incompatível com
/// "nenhuma pergunta ao usuário" e o tempo de resposta esperado de uma tela de
/// diagnóstico (ver Design Notes da spec).
/// </summary>
public sealed class GeradorCargaSimulada
{
    /// <summary>
    /// Ocupa todos os núcleos lógicos (<see cref="Environment.ProcessorCount"/>)
    /// com trabalho de CPU puro pela duração informada (ou até
    /// <paramref name="cancellationToken"/> ser cancelado) — o bastante para a
    /// leitura de sensores seguinte capturar uma temperatura sob carga real.
    /// Nunca inicia processo externo nem grava/lê disco. Roda via
    /// <see cref="TaskCreationOptions.LongRunning"/> (thread dedicada, fora do
    /// ThreadPool compartilhado) — corrigido na revisão independente: um laço
    /// ocupado nos núcleos lógicos no ThreadPool atrasaria outro trabalho
    /// assíncrono concorrente do processo.
    /// </summary>
    public Task GerarAsync(TimeSpan duracao, CancellationToken cancellationToken = default) =>
        Task.Factory.StartNew(() =>
        {
            var cronometro = Stopwatch.StartNew();

            Parallel.For(0, Environment.ProcessorCount, _ =>
            {
                double descarte = 0;

                // Checa o relógio a cada lote pequeno de iterações — mantém o
                // laço ocupado sem estourar muito além da duração pedida.
                while (cronometro.Elapsed < duracao && !cancellationToken.IsCancellationRequested)
                {
                    for (var i = 0; i < 2_000; i++)
                        descarte += Math.Sqrt(i + 1);
                }

                // Math.Sqrt de inteiro positivo nunca é NaN — este guard só existe
                // para impedir o JIT de eliminar o laço por "resultado não usado".
                if (double.IsNaN(descarte))
                    throw new InvalidOperationException("Estado impossível no gerador de carga simulada.");
            });

            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
