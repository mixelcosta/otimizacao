using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Backup;

/// <summary>Metadados de um backup criado antes de qualquer alteração.</summary>
public sealed record Backup
{
    public required string Id { get; init; }

    public required string Caminho { get; init; }

    public required string Checksum { get; init; }

    public DateTimeOffset CriadoEm { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Backup íntegro e gravado. O executor exige isto antes de aplicar.</summary>
    public bool Confirmado { get; init; }
}

/// <summary>
/// Backup obrigatório e bloqueante. Sem backup confirmado, nenhuma alteração
/// prossegue (regra invariante). No Windows real, complementaria com ponto de
/// restauração e export de serviços/energia/registro; no MVP multiplataforma,
/// persiste um snapshot íntegro do inventário e do contexto.
/// </summary>
public interface IServicoBackup
{
    Task<Resultado<Backup>> CriarBackupAsync(Inventario inventario, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IServicoBackup"/>
public sealed class ServicoBackup : IServicoBackup
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _diretorioBase;

    public ServicoBackup(string? diretorioBase = null)
    {
        _diretorioBase = diretorioBase
            ?? Path.Combine(AppContext.BaseDirectory, "data", "backups");
    }

    public async Task<Resultado<Backup>> CriarBackupAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        try
        {
            var id = $"bkp-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            var pasta = Path.Combine(_diretorioBase, id);
            Directory.CreateDirectory(pasta);

            var caminho = Path.Combine(pasta, "inventario.json");
            var conteudo = JsonSerializer.Serialize(inventario, Json);
            await File.WriteAllTextAsync(caminho, conteudo, cancellationToken).ConfigureAwait(false);

            var checksum = Checksum(conteudo);
            await File.WriteAllTextAsync(
                Path.Combine(pasta, "checksum.sha256"), checksum, cancellationToken).ConfigureAwait(false);

            // Confirmação: o arquivo existe e o checksum confere com o conteúdo relido.
            var relido = await File.ReadAllTextAsync(caminho, cancellationToken).ConfigureAwait(false);
            var integro = Checksum(relido) == checksum;

            var backup = new Backup
            {
                Id = id,
                Caminho = caminho,
                Checksum = checksum,
                Confirmado = integro,
            };

            return integro
                ? Resultado<Backup>.Ok(backup)
                : Resultado<Backup>.Falhar("Falha de integridade ao confirmar o backup.");
        }
        catch (IOException ex)
        {
            return Resultado<Backup>.Falhar($"Falha de E/S ao criar backup: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Resultado<Backup>.Falhar($"Sem permissão para criar backup: {ex.Message}");
        }
    }

    private static string Checksum(string conteudo)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
