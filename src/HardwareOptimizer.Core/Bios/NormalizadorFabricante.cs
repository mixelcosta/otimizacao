namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Limpa e padroniza as strings sujas do SMBIOS (passo "Normalização" do
/// fluxo_bios). Ex.: "ASUSTeK Computer Inc." → "ASUS". Gera uma chave de busca
/// estável para o lookup do fabricante e o cache.
/// </summary>
public static class NormalizadorFabricante
{
    // Correspondência exata (após lower) para siglas curtas e ambíguas.
    private static readonly IReadOnlyDictionary<string, string> Exatos =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hp"] = "HP",
            ["msi"] = "MSI",
            ["asus"] = "ASUS",
        };

    // Correspondência por fragmento contido na string suja.
    private static readonly (string Fragmento, string Canonico)[] Fragmentos =
    {
        ("asustek", "ASUS"),
        ("asus", "ASUS"),
        ("gigabyte", "Gigabyte"),
        ("giga-byte", "Gigabyte"),
        ("micro-star", "MSI"),
        ("msi", "MSI"),
        ("asrock", "ASRock"),
        ("hewlett", "HP"),
        ("packard", "HP"),
        ("lenovo", "Lenovo"),
        ("dell", "Dell"),
        ("acer", "Acer"),
        ("biostar", "Biostar"),
        ("intel", "Intel"),
    };

    public static string Normalizar(string? fabricante)
    {
        if (string.IsNullOrWhiteSpace(fabricante))
        {
            return "Desconhecido";
        }

        var limpo = fabricante.Trim();
        var lower = limpo.ToLowerInvariant();

        if (Exatos.TryGetValue(lower, out var exato))
        {
            return exato;
        }

        foreach (var (fragmento, canonico) in Fragmentos)
        {
            if (lower.Contains(fragmento, StringComparison.Ordinal))
            {
                return canonico;
            }
        }

        return limpo;
    }

    /// <summary>Gera a chave de busca "fabricante|modelo" normalizada e minúscula.</summary>
    public static string GerarChaveBusca(string? fabricante, string? modelo)
    {
        var fab = Normalizar(fabricante).ToLowerInvariant();
        var mod = ColapsarEspacos((modelo ?? string.Empty).Trim().ToLowerInvariant());
        return $"{fab}|{mod}";
    }

    private static string ColapsarEspacos(string texto) =>
        string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
