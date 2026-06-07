using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Cli;

/// <summary>
/// Provider de log em arquivo (append, thread-safe). Cada linha registra
/// timestamp, nível, categoria (classe) e mensagem — formato pensado para
/// análise posterior do ponto exato de falha. Não há provider de arquivo
/// embutido no Microsoft.Extensions.Logging, por isso este mínimo.
/// </summary>
public sealed class ArquivoLoggerProvider : ILoggerProvider
{
    private static readonly Encoding Utf8SemBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _caminho;
    private readonly LogLevel _minimo;
    private readonly object _trava = new();

    public ArquivoLoggerProvider(string caminho, LogLevel minimo = LogLevel.Debug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        _caminho = caminho;
        _minimo = minimo;

        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }
    }

    public ILogger CreateLogger(string categoryName) => new ArquivoLogger(categoryName, this, _minimo);

    public void Dispose()
    {
        // Sem recursos persistentes: cada escrita abre/fecha o arquivo.
    }

    private void Anexar(string linha)
    {
        lock (_trava)
        {
            File.AppendAllText(_caminho, linha + Environment.NewLine, Utf8SemBom);
        }
    }

    private sealed class ArquivoLogger : ILogger
    {
        private readonly string _categoria;
        private readonly ArquivoLoggerProvider _provider;
        private readonly LogLevel _minimo;

        public ArquivoLogger(string categoria, ArquivoLoggerProvider provider, LogLevel minimo)
        {
            _categoria = categoria;
            _provider = provider;
            _minimo = minimo;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimo && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var idx = _categoria.LastIndexOf('.');
            var categoriaCurta = idx >= 0 ? _categoria[(idx + 1)..] : _categoria;

            var sb = new StringBuilder(160);
            sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append(" [").Append(Nivel(logLevel)).Append("] ");
            sb.Append(categoriaCurta).Append(" - ");
            sb.Append(formatter(state, exception));

            if (exception is not null)
            {
                sb.Append(" | EXCEÇÃO ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            }

            _provider.Anexar(sb.ToString());
        }

        private static string Nivel(LogLevel nivel) => nivel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "?????",
        };
    }
}
