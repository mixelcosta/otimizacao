using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Drivers;

public sealed class AtualizadorDrivers
{
    private readonly IColetorHwid _coletor;
    private readonly IProvedorFonteOficial _provedor;
    private readonly ILogger<AtualizadorDrivers> _log;

    public AtualizadorDrivers(
        IColetorHwid coletor,
        IProvedorFonteOficial provedor,
        ILogger<AtualizadorDrivers> log)
    {
        _coletor = coletor;
        _provedor = provedor;
        _log = log;
    }

    public async Task<IReadOnlyList<InfoDriver>> VarrerAsync(CancellationToken ct = default)
    {
        var dispositivos = _coletor.Coletar();
        var resultado = new List<InfoDriver>();

        foreach (var dev in dispositivos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var disponivel = await _provedor.ConsultarAsync(dev.HardwareId, ct);
                if (disponivel is null)
                {
                    resultado.Add(dev with { Status = StatusDriver.Desconhecido });
                    continue;
                }

                bool temAtualizacao = !string.IsNullOrEmpty(disponivel.VersaoDisponivel)
                    && disponivel.VersaoDisponivel != dev.VersaoAtual;

                resultado.Add(dev with
                {
                    VersaoDisponivel = disponivel.VersaoDisponivel,
                    UrlDownload = disponivel.UrlDownload,
                    CertificadoWhql = disponivel.CertificadoWhql,
                    Status = temAtualizacao ? StatusDriver.AtualizacaoDisponivel : StatusDriver.Atualizado,
                });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Falha ao consultar WHQL para HWID '{Id}'.", dev.HardwareId);
                resultado.Add(dev with { Status = StatusDriver.Desconhecido });
            }
        }

        return resultado;
    }

    /// <summary>
    /// Cria backup dos drivers atuais via pnputil antes de qualquer instalação.
    /// </summary>
    public async Task<Resultado> ExportarBackupAsync(string pastaDestino, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(pastaDestino);
            var processo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/export-driver * \"{pastaDestino}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(processo);
            if (proc is null) return Resultado.Falhar("Não foi possível iniciar pnputil.exe");

            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0
                ? Resultado.Ok()
                : Resultado.Falhar($"pnputil /export-driver retornou código {proc.ExitCode}.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha no backup de drivers.");
            return Resultado.Falhar(ex.Message);
        }
    }

    /// <summary>
    /// Instala um driver via pnputil /add-driver (requer elevação de privilégio).
    /// </summary>
    public async Task<Resultado> InstalarAsync(string caminhoInf, CancellationToken ct = default)
    {
        if (!File.Exists(caminhoInf))
            return Resultado.Falhar($"Arquivo de driver não encontrado: {caminhoInf}");

        try
        {
            // Verb="runas" só é honrado pelo Windows quando UseShellExecute=true —
            // com false (necessário pra RedirectStandardOutput/Error), a elevação
            // é silenciosamente ignorada e o pnputil roda sem privilégio, falhando
            // sem UAC. Elevação é obrigatória aqui, então abrimos mão da captura de
            // saída (não suportada com UseShellExecute=true).
            var processo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/add-driver \"{caminhoInf}\" /install",
                UseShellExecute = true,
                Verb = "runas",
            };

            using var proc = System.Diagnostics.Process.Start(processo);
            if (proc is null) return Resultado.Falhar("Não foi possível iniciar pnputil.exe");

            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0
                ? Resultado.Ok()
                : Resultado.Falhar($"pnputil /add-driver retornou código {proc.ExitCode}.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao instalar driver '{Arquivo}'.", caminhoInf);
            return Resultado.Falhar(ex.Message);
        }
    }

    /// <summary>
    /// Rollback: reinstala os drivers a partir de um backup exportado previamente
    /// por <see cref="ExportarBackupAsync"/>. Acionado explicitamente pelo usuário
    /// — nunca automático. Reaproveita o mesmo padrão de <see cref="InstalarAsync"/>,
    /// mas aponta o pnputil para todos os .inf do diretório de backup.
    /// </summary>
    public async Task<Resultado> RestaurarBackupAsync(string caminhoBackup, CancellationToken ct = default)
    {
        if (!Directory.Exists(caminhoBackup))
            return Resultado.Falhar(
                $"Backup não encontrado em '{caminhoBackup}'. O diretório pode ter sido removido do disco.");

        try
        {
            // Ver nota em InstalarAsync: Verb="runas" exige UseShellExecute=true.
            // /subdirs é obrigatório aqui: ExportarBackupAsync usa "pnputil
            // /export-driver *", que grava cada pacote em uma subpasta numerada
            // própria (ex. backup\0\oem12.inf, backup\1\oem13.inf) — sem /subdirs
            // o glob "*.inf" na raiz do backup não encontra nada e o pnputil
            // retorna sucesso sem restaurar driver nenhum.
            var processo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/add-driver \"{Path.Combine(caminhoBackup, "*.inf")}\" /subdirs /install",
                UseShellExecute = true,
                Verb = "runas",
            };

            using var proc = System.Diagnostics.Process.Start(processo);
            if (proc is null) return Resultado.Falhar("Não foi possível iniciar pnputil.exe");

            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0
                ? Resultado.Ok()
                : Resultado.Falhar($"pnputil /add-driver (restauração) retornou código {proc.ExitCode}.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao restaurar backup de drivers '{Caminho}'.", caminhoBackup);
            return Resultado.Falhar(ex.Message);
        }
    }
}
