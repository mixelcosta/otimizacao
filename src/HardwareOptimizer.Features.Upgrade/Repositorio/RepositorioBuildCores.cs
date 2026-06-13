using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Features.Upgrade.Repositorio;

/// <summary>
/// Acessa o banco de dados público BuildCores Open DB no GitHub.
/// Repositório: https://github.com/buildcores/buildcores-open-db
/// Usa GitHub Search API para encontrar componentes por nome, depois
/// busca o JSON raw para extrair as specs relevantes.
/// Cache em memória com TTL de 30 minutos por query.
/// </summary>
public sealed class RepositorioBuildCores
{
    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "HardwareOptimizer/1.0" },
            { "Accept", "application/vnd.github.v3+json" },
        },
        Timeout = TimeSpan.FromSeconds(15),
    };

    private static readonly Dictionary<string, (DateTimeOffset Expira, string Resultado)> _cache = new();
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(30);

    private readonly ILogger _log;

    public RepositorioBuildCores(ILogger? logger = null) =>
        _log = logger ?? NullLogger.Instance;

    /// <summary>
    /// Pesquisa um componente no BuildCores DB por categoria e query textual.
    /// Retorna as specs formatadas para inclusão no prompt do Claude, ou null se não encontrar.
    /// </summary>
    public async Task<string?> PesquisarAsync(
        string categoria, string query, CancellationToken cancellationToken = default)
    {
        var chave = $"{categoria}:{ExtrairTermoChave(query)}".ToUpperInvariant();
        if (_cache.TryGetValue(chave, out var cached) && cached.Expira > DateTimeOffset.Now)
            return cached.Resultado;

        var uuid = await BuscarUuidAsync(categoria, query, cancellationToken).ConfigureAwait(false);
        if (uuid is null) return null;

        var specs = await BuscarSpecsAsync(categoria, uuid, cancellationToken).ConfigureAwait(false);
        if (specs is null) return null;

        var texto = FormatarSpecs(specs, categoria);
        _cache[chave] = (DateTimeOffset.Now.Add(_ttl), texto);
        return texto;
    }

    // ── GitHub Search → UUID ───────────────────────────────────────────────

    private async Task<string?> BuscarUuidAsync(
        string categoria, string query, CancellationToken ct)
    {
        var termo = ExtrairTermoChave(query);
        if (string.IsNullOrWhiteSpace(termo)) return null;

        // GitHub code search: busca o termo dentro dos arquivos da categoria
        var url = $"https://api.github.com/search/code" +
                  $"?q={Uri.EscapeDataString($"\"{termo}\" repo:buildcores/buildcores-open-db path:open-db/{categoria}")}";

        try
        {
            var resp = await _http.GetFromJsonAsync<GitHubSearchResult>(url, ct).ConfigureAwait(false);
            if (resp?.Items is { Count: > 0 } items)
            {
                // Extrai UUID do path: "open-db/CPU/xxxxxxxx-xxxx-....json"
                var path = items[0].Path;
                var filename = System.IO.Path.GetFileNameWithoutExtension(path);
                if (Guid.TryParse(filename, out _))
                {
                    _log.LogDebug("BuildCores: encontrado {UUID} para '{Termo}'.", filename, termo);
                    return filename;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "BuildCores: falha na busca GitHub Search API (rate limit?).");
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "BuildCores: erro ao parsear resposta do GitHub Search.");
        }

        return null;
    }

    // ── Raw content fetch ──────────────────────────────────────────────────

    private async Task<JsonDocument?> BuscarSpecsAsync(
        string categoria, string uuid, CancellationToken ct)
    {
        var url = $"https://raw.githubusercontent.com/buildcores/buildcores-open-db/main/open-db/{categoria}/{uuid}.json";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _log.LogWarning(ex, "BuildCores: falha ao buscar raw JSON para {UUID}.", uuid);
            return null;
        }
    }

    // ── Formatação das specs ───────────────────────────────────────────────

    private static string FormatarSpecs(JsonDocument doc, string categoria)
    {
        var root = doc.RootElement;
        var sb = new System.Text.StringBuilder();

        AppendSe(sb, root, "name", "Nome");
        AppendSe(sb, root, "brand", "Marca");
        AppendSe(sb, root, "series", "Série");

        switch (categoria)
        {
            case "CPU":
                AppendSe(sb, root, "socket", "Socket");
                AppendSe(sb, root, "cores", "Núcleos");
                AppendSe(sb, root, "threads", "Threads");
                AppendSe(sb, root, "base_clock", "Clock Base");
                AppendSe(sb, root, "boost_clock", "Clock Boost");
                AppendSe(sb, root, "tdp", "TDP");
                AppendSe(sb, root, "memory_type", "Tipo RAM suportado");
                break;
            case "GPU":
                AppendSe(sb, root, "tdp", "TDP");
                AppendSe(sb, root, "vram", "VRAM");
                AppendSe(sb, root, "pcie", "Slot PCIe");
                AppendSe(sb, root, "boost_clock", "Clock Boost");
                break;
            case "RAM":
                AppendSe(sb, root, "capacity", "Capacidade");
                AppendSe(sb, root, "speed", "Velocidade");
                AppendSe(sb, root, "ddr_type", "Tipo DDR");
                AppendSe(sb, root, "form_factor", "Form Factor");
                AppendSe(sb, root, "part_number", "Part Number");
                break;
            case "Motherboard":
                AppendSe(sb, root, "socket", "Socket");
                AppendSe(sb, root, "chipset", "Chipset");
                AppendSe(sb, root, "memory_type", "Tipo RAM");
                AppendSe(sb, root, "memory_slots", "Slots RAM");
                AppendSe(sb, root, "memory_max_gb", "RAM Máx (GB)");
                AppendSe(sb, root, "form_factor", "Form Factor");
                break;
            case "Storage":
                AppendSe(sb, root, "capacity", "Capacidade");
                AppendSe(sb, root, "interface", "Interface");
                AppendSe(sb, root, "read_speed", "Leitura");
                AppendSe(sb, root, "write_speed", "Escrita");
                break;
            case "PSU":
                AppendSe(sb, root, "wattage", "Wattage");
                AppendSe(sb, root, "efficiency", "Eficiência");
                AppendSe(sb, root, "modular", "Modular");
                break;
        }

        return sb.ToString();
    }

    private static void AppendSe(System.Text.StringBuilder sb, JsonElement root, string campo, string label)
    {
        if (root.TryGetProperty(campo, out var prop) && prop.ValueKind != JsonValueKind.Null)
            sb.AppendLine($"  {label}: {prop}");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Extrai o modelo/nome mais específico da query do usuário.</summary>
    private static string ExtrairTermoChave(string query)
    {
        // Remove palavras genéricas para focar no modelo
        var stopWords = new[] { "quero", "trocar", "upgrade", "minha", "meu", "para", "por", "uma", "um", "novo", "nova", "comprar" };
        var palavras = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 2 && !stopWords.Contains(p.ToLowerInvariant()))
            .ToArray();

        // Prefere termos que parecem nomes de modelos (contêm números ou letras maiúsculas)
        var modelo = palavras.FirstOrDefault(p => p.Any(char.IsDigit) || p.Any(char.IsUpper));
        return modelo ?? (palavras.Length > 0 ? string.Join(" ", palavras.Take(3)) : string.Empty);
    }

    // ── DTOs ───────────────────────────────────────────────────────────────

    private sealed class GitHubSearchResult
    {
        public List<GitHubSearchItem>? Items { get; set; }
    }

    private sealed class GitHubSearchItem
    {
        public string Path { get; set; } = "";
    }
}
