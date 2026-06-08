using System.Diagnostics;

namespace HardwareOptimizer.Agent.Platform;

/// <summary>
/// Implementação real de <see cref="IExecutorProcesso"/> sobre
/// <see cref="Process"/>. Captura stdout/stderr sem risco de deadlock (leituras
/// assíncronas + espera com tempo limite) — os utilitários alvo (powercfg,
/// sc.exe) produzem saída pequena e terminam rápido.
/// </summary>
public sealed class ExecutorProcesso : IExecutorProcesso
{
    private readonly TimeSpan _tempoLimite;

    public ExecutorProcesso(TimeSpan? tempoLimite = null) =>
        _tempoLimite = tempoLimite ?? TimeSpan.FromSeconds(30);

    public ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivo);
        ArgumentNullException.ThrowIfNull(argumentos);

        var inicio = new ProcessStartInfo
        {
            FileName = arquivo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argumento in argumentos)
        {
            inicio.ArgumentList.Add(argumento);
        }

        using var processo = Process.Start(inicio)
            ?? throw new InvalidOperationException($"Não foi possível iniciar o processo '{arquivo}'.");

        // Lê de forma assíncrona para não bloquear caso ambos os fluxos encham.
        var leituraSaida = processo.StandardOutput.ReadToEndAsync();
        var leituraErro = processo.StandardError.ReadToEndAsync();

        if (!processo.WaitForExit((int)_tempoLimite.TotalMilliseconds))
        {
            try
            {
                if (!processo.HasExited)
                {
                    processo.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // O processo terminou entre a verificação e o Kill — nada a fazer.
            }

            throw new TimeoutException($"Processo '{arquivo}' excedeu o tempo limite de {_tempoLimite}.");
        }

        // Garante que as leituras assíncronas concluíram após a saída do processo.
        processo.WaitForExit();
        return new ResultadoProcesso(
            processo.ExitCode,
            leituraSaida.GetAwaiter().GetResult(),
            leituraErro.GetAwaiter().GetResult());
    }
}
