namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Fonte de informação de BIOS do fabricante. A implementação padrão é um banco
/// curado em memória; uma futura implementação pode buscar na web (priorizando
/// o domínio do fabricante) e cachear o resultado.
/// </summary>
public interface IProvedorInfoBios
{
    Task<InfoBiosFabricante?> ObterAsync(string chaveBusca, CancellationToken cancellationToken = default);
}

/// <summary>
/// Banco curado das placas mais comuns (passo "Verificação com fabricante", via
/// banco curado). Chaveado pela mesma chave de busca normalizada do inventário.
/// </summary>
public sealed class BancoCuradoBios : IProvedorInfoBios
{
    private readonly IReadOnlyDictionary<string, InfoBiosFabricante> _entradas;

    public BancoCuradoBios(IReadOnlyDictionary<string, InfoBiosFabricante>? entradas = null)
    {
        _entradas = entradas ?? Padrao();
    }

    public Task<InfoBiosFabricante?> ObterAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entradas.TryGetValue(chaveBusca, out var info);
        return Task.FromResult(info);
    }

    private static IReadOnlyDictionary<string, InfoBiosFabricante> Padrao() =>
        new Dictionary<string, InfoBiosFabricante>(StringComparer.OrdinalIgnoreCase)
        {
            ["asus|rog strix b550-f"] = new InfoBiosFabricante
            {
                Fabricante = "ASUS",
                Modelo = "ROG STRIX B550-F",
                VersaoMaisRecente = "3405",
                DataMaisRecente = "2023-06-01",
                Changelog = "Atualiza AGESA e melhora a estabilidade de memória e compatibilidade de CPU.",
                LinkManual = "https://www.asus.com/support/",
                Fonte = "https://www.asus.com/motherboards-components/motherboards/rog/rog-strix-b550-f-gaming/helpdesk_bios/",
                Ganho = GanhoEstimado.Medio,
                Motivo = "Correção de estabilidade de memória e compatibilidade de CPU.",
            },
            ["msi|mag b550 tomahawk"] = new InfoBiosFabricante
            {
                Fabricante = "MSI",
                Modelo = "MAG B550 TOMAHAWK",
                VersaoMaisRecente = "7C91vH9",
                DataMaisRecente = "2023-08-10",
                Changelog = "Atualiza AGESA ComboAM4v2PI; melhora compatibilidade com CPUs Ryzen 5000.",
                LinkManual = "https://www.msi.com/Motherboard/MAG-B550-TOMAHAWK/support",
                Fonte = "https://www.msi.com/Motherboard/MAG-B550-TOMAHAWK/support",
                Ganho = GanhoEstimado.Medio,
                Motivo = "Melhora de compatibilidade de CPU e estabilidade.",
            },
            ["gigabyte|b550 aorus elite"] = new InfoBiosFabricante
            {
                Fabricante = "Gigabyte",
                Modelo = "B550 AORUS ELITE",
                VersaoMaisRecente = "F16",
                DataMaisRecente = "2022-11-20",
                Changelog = "Atualiza AGESA; correções gerais de estabilidade.",
                LinkManual = "https://www.gigabyte.com/Motherboard/B550-AORUS-ELITE-rev-10/support",
                Fonte = "https://www.gigabyte.com/Motherboard/B550-AORUS-ELITE-rev-10/support",
                Ganho = GanhoEstimado.Baixo,
                Motivo = "Correções gerais de estabilidade.",
            },
        };
}
