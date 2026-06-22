using System.Text.Json;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Drivers;

/// <summary>
/// Repositório WHQL offline com catálogo JSON embarcado no assembly.
/// Produção: substituir por chamada REST à API WHQL.
/// </summary>
public sealed class RepositorioWhqlEstatico : IRepositorioDriversWhql
{
    private readonly IReadOnlyDictionary<string, InfoDriver> _catalogo;

    public RepositorioWhqlEstatico()
    {
        _catalogo = CarregarCatalogo();
    }

    public Task<InfoDriver?> ConsultarAsync(string hardwareId, CancellationToken ct = default)
    {
        var upper = hardwareId.ToUpperInvariant();

        // 1. Busca exata (case-insensitive)
        if (_catalogo.TryGetValue(upper, out var exato))
            return Task.FromResult<InfoDriver?>(exato);

        // 2. Busca por prefixo simples (parte antes do primeiro &)
        var prefixo = ExtrairPrefixo(hardwareId);
        if (prefixo is not null && _catalogo.TryGetValue(prefixo, out var porPrefixo))
            return Task.FromResult<InfoDriver?>(porPrefixo);

        // 3. Scan: chave do catálogo que seja prefixo do HWID completo
        //    (para entradas compostas como "HDAUDIO\FUNC_01&VEN_10EC")
        foreach (var (chave, valor) in _catalogo)
        {
            if (upper.StartsWith(chave, StringComparison.Ordinal))
                return Task.FromResult<InfoDriver?>(valor);
        }

        return Task.FromResult<InfoDriver?>(null);
    }

    public static string? ExtrairPrefixo(string hardwareId)
    {
        var idx = hardwareId.IndexOf('&');
        return idx > 0 ? hardwareId[..idx].ToUpperInvariant() : null;
    }

    private static IReadOnlyDictionary<string, InfoDriver> CarregarCatalogo()
    {
        try
        {
            var assembly = typeof(RepositorioWhqlEstatico).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("whql_catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName is null) return new Dictionary<string, InfoDriver>();

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            var entradas = JsonSerializer.Deserialize<List<EntradaCatalogo>>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? [];

            return entradas.ToDictionary(
                e => e.HardwareIdPrefixo.ToUpperInvariant(),
                e => new InfoDriver
                {
                    HardwareId = e.HardwareIdPrefixo,
                    Descricao = e.Descricao,
                    Fabricante = e.Fabricante,
                    VersaoDisponivel = e.VersaoDisponivel,
                    UrlDownload = e.UrlDownload,
                    CertificadoWhql = e.CertificadoWhql,
                    Status = StatusDriver.Desconhecido,
                });
        }
        catch
        {
            return new Dictionary<string, InfoDriver>();
        }
    }

    private sealed class EntradaCatalogo
    {
        public string HardwareIdPrefixo { get; init; } = string.Empty;
        public string Descricao { get; init; } = string.Empty;
        public string Fabricante { get; init; } = string.Empty;
        public string? VersaoDisponivel { get; init; }
        public string? UrlDownload { get; init; }
        public bool CertificadoWhql { get; init; }
    }
}
