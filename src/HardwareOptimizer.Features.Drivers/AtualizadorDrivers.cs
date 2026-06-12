using System.Runtime.Versioning;
using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Drivers;

public sealed class AtualizadorDrivers
{
    private readonly ColetorHwid _coletor;
    private readonly IRepositorioDriversWhql _repositorio;
    private readonly ILogger<AtualizadorDrivers> _log;

    public AtualizadorDrivers(
        ColetorHwid coletor,
        IRepositorioDriversWhql repositorio,
        ILogger<AtualizadorDrivers> log)
    {
        _coletor = coletor;
        _repositorio = repositorio;
        _log = log;
    }

    [SupportedOSPlatform("windows")]
    public async Task<IReadOnlyList<InfoDriver>> VarrerAsync(CancellationToken ct = default)
    {
        var dispositivos = _coletor.Coletar();
        var resultado = new List<InfoDriver>();

        foreach (var dev in dispositivos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var disponivel = await _repositorio.ConsultarAsync(dev.HardwareId, ct);
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
            var processo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/add-driver \"{caminhoInf}\" /install",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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
}
