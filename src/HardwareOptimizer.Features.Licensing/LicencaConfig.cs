namespace HardwareOptimizer.Features.Licensing;

/// <summary>
/// Configurações globais da licença. Preencha com os dados do seu produto no LemonSqueezy.
/// </summary>
public static class LicencaConfig
{
    // ─── LemonSqueezy ──────────────────────────────────────────────────────────
    // Após criar seu produto no LemonSqueezy, substitua pela URL real do checkout.
    // Exemplo: https://sualoja.lemonsqueezy.com/buy/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    public const string UrlCompra = "https://otimizebuilder.lemonsqueezy.com/buy/PRODUCT_ID";

    // ─── Tolerância offline ────────────────────────────────────────────────────
    // Número de dias que o app funciona sem internet antes de suspender o Premium.
    public const int DiasGracePeriodo = 7;
}
