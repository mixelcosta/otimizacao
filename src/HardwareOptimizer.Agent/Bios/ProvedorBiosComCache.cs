using System.Text.Json;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Bios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger _log;

    public ProvedorBiosComCache(
        IProvedorInfoBios interno, IRepositorioOtimizacao repositorio, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(interno);
        ArgumentNullException.ThrowIfNull(repositorio);
        _interno = interno;
        _repositorio = repositorio;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<InfoBiosFabricante?> ObterAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);

        var cacheJson = await _repositorio.ObterCacheBiosAsync(chaveBusca, cancellationToken).ConfigureAwait(false);
        if (cacheJson is not null)
        {
            var emCache = Desserializar(chaveBusca, cacheJson);
            if (emCache is not null)
            {
                _log.LogDebug("BIOS cache HIT para '{Chave}'.", chaveBusca);
                return emCache;
            }
        }

        _log.LogDebug("BIOS cache MISS para '{Chave}'; consultando provedor interno.", chaveBusca);
        var info = await _interno.ObterAsync(chaveBusca, cancellationToken).ConfigureAwait(false);
        if (info is not null)
        {
            await _repositorio
                .SalvarCacheBiosAsync(chaveBusca, JsonSerializer.Serialize(info, Json), cancellationToken)
                .ConfigureAwait(false);
            _log.LogDebug("BIOS cache atualizado para '{Chave}'.", chaveBusca);
        }

        return info;
    }

    private InfoBiosFabricante? Desserializar(string chaveBusca, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<InfoBiosFabricante>(json, Json);
        }
        catch (JsonException ex)
        {
            // Cache corrompido: ignora e recorre ao provedor interno.
            _log.LogWarning(ex, "BIOS cache corrompido para '{Chave}'; recorrendo ao provedor interno.", chaveBusca);
            return null;
        }
    }
}
