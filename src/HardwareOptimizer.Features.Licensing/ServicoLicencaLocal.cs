using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Licensing;

/// <summary>
/// Valida licença por HMAC-SHA256 da chave contra o ID do hardware local.
/// O arquivo license.dat armazena o JSON cifrado com DPAPI (Windows).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServicoLicencaLocal : IServicoLicenca
{
    private static readonly string ArquivoLicenca = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OtimizeBuilder", "license.dat");

    private readonly ILogger<ServicoLicencaLocal> _log;
    private readonly ValidadorChaveLicenca _validador;
    private TipoLicenca _tipoAtual = TipoLicenca.Gratuita;

    public ServicoLicencaLocal(ILogger<ServicoLicencaLocal> log)
        : this(log, new ValidadorChaveLicenca()) { }

    internal ServicoLicencaLocal(ILogger<ServicoLicencaLocal> log, ValidadorChaveLicenca validador)
    {
        _log = log;
        _validador = validador;
        CarregarDoDisco();
    }

    public TipoLicenca TipoAtual => _tipoAtual;

    public bool TemAcesso(FuncionalidadePremium funcionalidade) =>
        _tipoAtual == TipoLicenca.Premium;

    public async Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chave))
            return ResultadoAtivacao.Falhar("Chave não pode ser vazia.");

        var machineId = ObterIdMaquina();
        if (!_validador.ChaveValida(chave, machineId))
        {
            _log.LogWarning("Tentativa de ativação com chave inválida.");
            return ResultadoAtivacao.Falhar("Chave inválida para esta máquina.");
        }

        try
        {
            var hashEsperado = ComputarHashChave(chave);
            var registro = new RegistroLicenca
            {
                Hash = hashEsperado,
                Tipo = TipoLicenca.Premium,
                AtivoEm = DateTimeOffset.UtcNow,
            };
            await SalvarNoDisco(registro, ct);
            _tipoAtual = TipoLicenca.Premium;
            _log.LogInformation("Licença Premium ativada.");
            return ResultadoAtivacao.Ok(TipoLicenca.Premium);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao ativar licença.");
            return ResultadoAtivacao.Falhar("Erro ao salvar licença: " + ex.Message);
        }
    }

    public async Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(ArquivoLicenca))
                File.Delete(ArquivoLicenca);
            _tipoAtual = TipoLicenca.Gratuita;
            await Task.CompletedTask;
            return ResultadoAtivacao.Ok(TipoLicenca.Gratuita);
        }
        catch (Exception ex)
        {
            return ResultadoAtivacao.Falhar(ex.Message);
        }
    }

    private void CarregarDoDisco()
    {
        if (!File.Exists(ArquivoLicenca))
            return;

        try
        {
            var cifrado = File.ReadAllBytes(ArquivoLicenca);
            var json = DescifrarDpapi(cifrado);
            var registro = JsonSerializer.Deserialize<RegistroLicenca>(json);
            if (registro?.Tipo == TipoLicenca.Premium && !string.IsNullOrEmpty(registro.Hash))
                _tipoAtual = TipoLicenca.Premium;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Não foi possível carregar licença do disco; usando Gratuita.");
        }
    }

    private static async Task SalvarNoDisco(RegistroLicenca registro, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ArquivoLicenca)!);
        var json = JsonSerializer.Serialize(registro);
        var cifrado = CifrarDpapi(json);
        await File.WriteAllBytesAsync(ArquivoLicenca, cifrado, ct);
    }

    private static string ComputarHashChave(string chave)
    {
        var maquinaId = ObterIdMaquina();
        var dados = Encoding.UTF8.GetBytes(chave + "|" + maquinaId);
        return Convert.ToBase64String(SHA256.HashData(dados));
    }

    private static string ObterIdMaquina()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString() ?? "fallback";
        }
        catch
        {
            return "fallback";
        }
    }

    private static byte[] CifrarDpapi(string texto)
    {
        var bytes = Encoding.UTF8.GetBytes(texto);
        return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    }

    private static string DescifrarDpapi(byte[] dados)
    {
        var bytes = ProtectedData.Unprotect(dados, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    private sealed record RegistroLicenca
    {
        public string? Hash { get; init; }
        public TipoLicenca Tipo { get; init; }
        public DateTimeOffset AtivoEm { get; init; }
    }
}
