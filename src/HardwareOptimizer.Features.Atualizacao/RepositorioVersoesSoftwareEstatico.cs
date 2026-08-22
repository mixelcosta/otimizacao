using System.Text.Json;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Repositório de versões de software offline com catálogo JSON embarcado no
/// assembly (~8 programas comuns). Mesmo padrão de <c>RepositorioWhqlEstatico</c>
/// (Features.Drivers) — sem ele, <c>IProvedorFonteOficial</c> sempre devolveria
/// "sem informação" para software, tornando o caminho de sucesso inalcançável
/// (mesmo problema já registrado em deferred-work.md para o catálogo de driver).
///
/// Busca por nome, case-insensitive, por substring: nomes de programa variam
/// mais entre instalações (ex. "7-Zip 24.07 (x64)") do que um Hardware ID, então
/// a busca exata usada em <c>RepositorioWhqlEstatico</c> não se aplica aqui.
/// </summary>
public sealed class RepositorioVersoesSoftwareEstatico : IRepositorioVersoesSoftware
{
    private readonly IReadOnlyList<EntradaCatalogo> _catalogo;

    public RepositorioVersoesSoftwareEstatico()
    {
        _catalogo = CarregarCatalogo();
    }

    public Task<InfoSoftware?> ConsultarAsync(string nomePrograma, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomePrograma))
            return Task.FromResult<InfoSoftware?>(null);

        var entrada = _catalogo.FirstOrDefault(e =>
            nomePrograma.Contains(e.Nome, StringComparison.OrdinalIgnoreCase));

        if (entrada is null)
            return Task.FromResult<InfoSoftware?>(null);

        return Task.FromResult<InfoSoftware?>(new InfoSoftware
        {
            Nome = entrada.Nome,
            VersaoDisponivel = entrada.VersaoDisponivel,
            UrlDownload = entrada.UrlDownload,
            // VersaoAtual/Status não fazem sentido aqui — esta é a entrada de
            // catálogo (fonte oficial), não o resultado final da comparação.
            // VerificadorSoftware monta o InfoSoftware definitivo.
        });
    }

    private static IReadOnlyList<EntradaCatalogo> CarregarCatalogo()
    {
        try
        {
            var assembly = typeof(RepositorioVersoesSoftwareEstatico).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("software_catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName is null) return [];

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            return JsonSerializer.Deserialize<List<EntradaCatalogo>>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class EntradaCatalogo
    {
        public string Nome { get; init; } = string.Empty;
        public string? VersaoDisponivel { get; init; }
        public string? UrlDownload { get; init; }
    }
}
