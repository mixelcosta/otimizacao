using System.Text.Json;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Bios;

namespace HardwareOptimizer.Agent.Bios;

/// <summary>
/// Decorador que adiciona cache em SQLite a qualquer <see cref="IProvedorInfoBios"/>.
/// Consulta o cache primeiro; em caso de falta, delega ao provedor interno
/// (banco curado ou, futuramente, busca web) e persiste o resultado.
/// </summary>
public sealed class ProvedorBiosComCache : IProvedorInfoBios
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly IProvedorInfoBios _interno;
    private readonly IRepositorioOtimizacao _repositorio;

    public ProvedorBiosComCache(IProvedorInfoBios interno, IRepositorioOtimizacao repositorio)
    {
        ArgumentNullException.ThrowIfNull(interno);
        ArgumentNullException.ThrowIfNull(repositorio);
        _interno = interno;
        _repositorio = repositorio;
    }

    public async Task<InfoBiosFabricante?> ObterAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);

        var cacheJson = await _repositorio.ObterCacheBiosAsync(chaveBusca, cancellationToken).ConfigureAwait(false);
        if (cacheJson is not null)
        {
            var emCache = Desserializar(cacheJson);
            if (emCache is not null)
            {
                return emCache;
            }
        }

        var info = await _interno.ObterAsync(chaveBusca, cancellationToken).ConfigureAwait(false);
        if (info is not null)
        {
            await _repositorio
                .SalvarCacheBiosAsync(chaveBusca, JsonSerializer.Serialize(info, Json), cancellationToken)
                .ConfigureAwait(false);
        }

        return info;
    }

    private static InfoBiosFabricante? Desserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<InfoBiosFabricante>(json, Json);
        }
        catch (JsonException)
        {
            return null; // Cache corrompido: ignora e recorre ao provedor interno.
        }
    }
}
