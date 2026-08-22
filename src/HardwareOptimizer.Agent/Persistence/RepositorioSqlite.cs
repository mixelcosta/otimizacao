using System.Text.Json;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Persistence;

/// <summary>Repositório SQLite. Abre uma conexão por operação a partir da connection string.</summary>
public sealed class RepositorioSqlite : IRepositorioOtimizacao
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _connectionString;
    private readonly ILogger _log;

    public RepositorioSqlite(string connectionString, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _log = logger ?? NullLogger.Instance;
    }

    /// <summary>Cria um repositório apontando para um arquivo de banco local.</summary>
    public static RepositorioSqlite DeArquivo(string caminhoArquivo, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoArquivo);
        var diretorio = Path.GetDirectoryName(Path.GetFullPath(caminhoArquivo));
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        return new RepositorioSqlite($"Data Source={caminhoArquivo}", logger);
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        _log.LogDebug("Inicializando esquema do banco SQLite ('{ConnectionString}').", _connectionString);
        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS inventarios (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                coletado_em TEXT NOT NULL,
                dados_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS consentimentos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome_perfil TEXT NOT NULL,
                versao_catalogo TEXT NOT NULL,
                registrado_em TEXT NOT NULL,
                checkboxes_json TEXT NOT NULL,
                valores_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS execucoes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                perfil_nome TEXT NOT NULL,
                sucesso INTEGER NOT NULL,
                executado_em TEXT NOT NULL,
                relatorio_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cache_bios (
                chave_busca TEXT PRIMARY KEY,
                dados_json TEXT NOT NULL,
                atualizado_em TEXT NOT NULL
            );
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> SalvarInventarioAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        _log.LogDebug("Persistindo inventário coletado em {Quando}.", inventario.ColetadoEm);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO inventarios (coletado_em, dados_json)
            VALUES ($coletado, $dados);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$coletado", inventario.ColetadoEm.ToString("O"));
        comando.Parameters.AddWithValue("$dados", JsonSerializer.Serialize(inventario, Json));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<long> RegistrarConsentimentoAsync(
        RegistroConsentimento registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        _log.LogInformation(
            "Registrando consentimento (auditoria): perfil '{Perfil}', catálogo {Versao}.",
            registro.NomePerfil, registro.VersaoCatalogo);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO consentimentos
                (nome_perfil, versao_catalogo, registrado_em, checkboxes_json, valores_json)
            VALUES ($perfil, $versao, $em, $checkboxes, $valores);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$perfil", registro.NomePerfil);
        comando.Parameters.AddWithValue("$versao", registro.VersaoCatalogo);
        comando.Parameters.AddWithValue("$em", registro.RegistradoEm.ToString("O"));
        comando.Parameters.AddWithValue("$checkboxes", JsonSerializer.Serialize(registro.CheckboxesMarcados, Json));
        comando.Parameters.AddWithValue("$valores", JsonSerializer.Serialize(registro.ValoresEscolhidos, Json));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<long> RegistrarExecucaoAsync(
        RelatorioExecucao relatorio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relatorio);
        _log.LogInformation(
            "Registrando execução: perfil '{Perfil}', sucesso={Sucesso}.", relatorio.PerfilNome, relatorio.Sucesso);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO execucoes (perfil_nome, sucesso, executado_em, relatorio_json)
            VALUES ($perfil, $sucesso, $em, $relatorio);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$perfil", relatorio.PerfilNome);
        comando.Parameters.AddWithValue("$sucesso", relatorio.Sucesso ? 1 : 0);
        comando.Parameters.AddWithValue("$em", DateTimeOffset.UtcNow.ToString("O"));
        comando.Parameters.AddWithValue("$relatorio", JsonSerializer.Serialize(relatorio, Json));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<string?> ObterCacheBiosAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT dados_json FROM cache_bios WHERE chave_busca = $chave;";
        comando.Parameters.AddWithValue("$chave", chaveBusca);
        var resultado = await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return resultado as string;
    }

    public async Task SalvarCacheBiosAsync(
        string chaveBusca, string dadosJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);
        ArgumentNullException.ThrowIfNull(dadosJson);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO cache_bios (chave_busca, dados_json, atualizado_em)
            VALUES ($chave, $dados, $em)
            ON CONFLICT(chave_busca) DO UPDATE SET dados_json = $dados, atualizado_em = $em;
            """;
        comando.Parameters.AddWithValue("$chave", chaveBusca);
        comando.Parameters.AddWithValue("$dados", dadosJson);
        comando.Parameters.AddWithValue("$em", DateTimeOffset.UtcNow.ToString("O"));
        await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<long> ContarInventariosAsync(CancellationToken cancellationToken = default) =>
        ContarAsync("inventarios", cancellationToken);

    public Task<long> ContarConsentimentosAsync(CancellationToken cancellationToken = default) =>
        ContarAsync("consentimentos", cancellationToken);

    public Task<long> ContarExecucoesAsync(CancellationToken cancellationToken = default) =>
        ContarAsync("execucoes", cancellationToken);

    private async Task<long> ContarAsync(string tabela, CancellationToken cancellationToken)
    {
        var commandText = tabela switch
        {
            "inventarios"    => "SELECT COUNT(*) FROM inventarios;",
            "consentimentos" => "SELECT COUNT(*) FROM consentimentos;",
            "execucoes"      => "SELECT COUNT(*) FROM execucoes;",
            _ => throw new ArgumentOutOfRangeException(nameof(tabela), tabela, "Tabela não mapeada em ContarAsync."),
        };

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = commandText;
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<SqliteConnection> AbrirAsync(CancellationToken cancellationToken)
    {
        var conexao = new SqliteConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conexao;
    }
}
