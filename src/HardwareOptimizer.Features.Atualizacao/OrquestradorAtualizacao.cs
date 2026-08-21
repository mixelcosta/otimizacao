using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Drivers;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Ponto único que liga varredura, backup, instalação e rollback de drivers.
/// Consumidores (RoteadorIpc, ViewModels) só falam com esta classe — nunca com
/// <see cref="AtualizadorDrivers"/>/<c>IRepositorioDriversWhql</c> diretamente
/// (ver Boundaries da spec-1-2-driver-scan-aprovacao-rollback).
///
/// A varredura reaproveita <see cref="AtualizadorDrivers.VarrerAsync"/> — que já
/// consulta o catálogo WHQL e devolve <see cref="InfoDriver"/> completo (versão,
/// URL de download, certificação). É o mesmo catálogo que
/// <see cref="ProvedorFonteOficialDriver"/> usa por trás de
/// <see cref="IProvedorFonteOficial"/>; reduzir a varredura em massa a esse
/// contrato mais fino perderia dados hoje exibidos na tela (URL de download,
/// selo WHQL), por isso não é usado aqui — <see cref="IProvedorFonteOficial"/>
/// fica disponível como a fronteira oficial para futuras severidades (BIOS,
/// aplicativos) e para consultas pontuais de versão.
/// </summary>
public sealed class OrquestradorAtualizacao
{
    private readonly AtualizadorDrivers _atualizador;

    public OrquestradorAtualizacao(AtualizadorDrivers atualizador)
    {
        _atualizador = atualizador;
    }

    public Task<IReadOnlyList<InfoDriver>> VarrerAsync(CancellationToken ct = default) =>
        _atualizador.VarrerAsync(ct);

    /// <summary>
    /// Cria backup dos drivers atuais em um diretório com carimbo de tempo,
    /// sob <c>%LocalAppData%\OtimizeBuilder\DriverBackups</c>. Deve ser chamado
    /// e ter sucesso ANTES de qualquer <see cref="InstalarAsync"/> (Boundaries §Always).
    /// </summary>
    public async Task<ResultadoBackup> BackupAsync(CancellationToken ct = default)
    {
        var pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OtimizeBuilder", "DriverBackups",
            DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

        var resultado = await _atualizador.ExportarBackupAsync(pasta, ct).ConfigureAwait(false);

        return new ResultadoBackup
        {
            Sucesso = resultado.Sucesso,
            Erro = resultado.Sucesso ? null : resultado.MensagemErro,
            CaminhoBackup = resultado.Sucesso ? pasta : null,
        };
    }

    public Task<Resultado> InstalarAsync(string caminhoInf, CancellationToken ct = default) =>
        _atualizador.InstalarAsync(caminhoInf, ct);

    /// <summary>
    /// Rollback acionado pelo usuário: reinstala a partir de um backup exportado
    /// anteriormente. Nunca automático/silencioso (Boundaries §Never).
    /// </summary>
    public Task<Resultado> ReverterAsync(string caminhoBackup, CancellationToken ct = default) =>
        _atualizador.RestaurarBackupAsync(caminhoBackup, ct);
}

/// <summary>Resultado de <see cref="OrquestradorAtualizacao.BackupAsync"/>.</summary>
public sealed record ResultadoBackup
{
    public required bool Sucesso { get; init; }

    public string? Erro { get; init; }

    public string? CaminhoBackup { get; init; }
}
