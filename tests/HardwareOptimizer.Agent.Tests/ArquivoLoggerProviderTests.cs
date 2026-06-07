using HardwareOptimizer.Cli;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ArquivoLoggerProviderTests : IDisposable
{
    private readonly string _arquivo;

    public ArquivoLoggerProviderTests()
    {
        _arquivo = Path.Combine(Path.GetTempPath(), "hwopt-log-" + Guid.NewGuid().ToString("N") + ".log");
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_arquivo);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    [Fact]
    public void Logger_escreve_linha_com_nivel_categoria_e_mensagem()
    {
        using (var provider = new ArquivoLoggerProvider(_arquivo, LogLevel.Debug))
        {
            var logger = provider.CreateLogger("HardwareOptimizer.Agent.Execution.ExecutorControlado");
            logger.LogWarning("Categoria {Categoria} BLOQUEADA: {Motivo}", "CPU", "backup");
        }

        var conteudo = File.ReadAllText(_arquivo);
        Assert.Contains("WARN", conteudo, StringComparison.Ordinal);
        Assert.Contains("ExecutorControlado", conteudo, StringComparison.Ordinal); // categoria curta (sem namespace)
        Assert.Contains("Categoria CPU BLOQUEADA: backup", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_inclui_detalhe_da_excecao()
    {
        using (var provider = new ArquivoLoggerProvider(_arquivo, LogLevel.Debug))
        {
            var logger = provider.CreateLogger("X");
            logger.LogError(new InvalidOperationException("falha simulada"), "erro ao processar");
        }

        var conteudo = File.ReadAllText(_arquivo);
        Assert.Contains("InvalidOperationException", conteudo, StringComparison.Ordinal);
        Assert.Contains("falha simulada", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_respeita_nivel_minimo()
    {
        using (var provider = new ArquivoLoggerProvider(_arquivo, LogLevel.Warning))
        {
            var logger = provider.CreateLogger("X");
            logger.LogDebug("abaixo do minimo");
            logger.LogError("acima do minimo");
        }

        var conteudo = File.Exists(_arquivo) ? File.ReadAllText(_arquivo) : string.Empty;
        Assert.DoesNotContain("abaixo do minimo", conteudo, StringComparison.Ordinal);
        Assert.Contains("acima do minimo", conteudo, StringComparison.Ordinal);
    }
}
