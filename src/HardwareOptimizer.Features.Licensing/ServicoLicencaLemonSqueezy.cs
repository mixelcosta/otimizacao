using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.Licensing;

/// <summary>
/// Valida licenças em tempo real via API do LemonSqueezy.
/// Armazena o resultado localmente em DPAPI para funcionar offline dentro do grace period.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServicoLicencaLemonSqueezy : IServicoLicenca, IDisposable
{
    private const string ApiBase = "https://api.lemonsqueezy.com/v1/licenses";

    private static readonly string ArquivoLicenca = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OtimizeBuilder", "license.dat");

    private readonly ILogger<ServicoLicencaLemonSqueezy> _log;
    private readonly HttpClient _http;
    private TipoLicenca _tipoAtual = TipoLicenca.Gratuita;
    private RegistroLicenca? _registro;

    public ServicoLicencaLemonSqueezy(ILogger<ServicoLicencaLemonSqueezy> log)
    {
        _log = log;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        CarregarDoDisco();
    }

    public TipoLicenca TipoAtual => _tipoAtual;
    public string? NomeCliente => _registro?.NomeCliente;
    public string? EmailCliente => _registro?.EmailCliente;

    public bool TemAcesso(FuncionalidadePremium _) => _tipoAtual == TipoLicenca.Premium;

    public async Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chave))
            return ResultadoAtivacao.Falhar("Chave não pode ser vazia.");

        try
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("license_key", chave.Trim()),
                new KeyValuePair<string, string>("instance_name", Environment.MachineName ?? "PC"),
            });

            var resp = await _http.PostAsync($"{ApiBase}/activate", form, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("activated", out var ap) || !ap.GetBoolean())
            {
                var erro = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                return ResultadoAtivacao.Falhar(erro ?? "Ativação recusada pelo servidor.");
            }

            var instanceId = root.GetProperty("instance").GetProperty("id").GetString()!;
            var meta = root.GetProperty("meta");
            var nome = meta.TryGetProperty("customer_name", out var cn) ? cn.GetString() : null;
            var email = meta.TryGetProperty("customer_email", out var ce) ? ce.GetString() : null;

            var registro = new RegistroLicenca
            {
                Chave = chave.Trim(),
                InstanceId = instanceId,
                Tipo = TipoLicenca.Premium,
                AtivoEm = DateTimeOffset.UtcNow,
                UltimaValidacao = DateTimeOffset.UtcNow,
                NomeCliente = nome,
                EmailCliente = email,
            };

            await SalvarNoDisco(registro, ct);
            _registro = registro;
            _tipoAtual = TipoLicenca.Premium;
            _log.LogInformation("Licença Premium ativada para {Nome}.", nome);
            return ResultadoAtivacao.Ok(TipoLicenca.Premium, nome, email);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Sem conexão ao ativar licença.");
            return ResultadoAtivacao.Falhar("Sem conexão com o servidor. Verifique sua internet.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao ativar licença.");
            return ResultadoAtivacao.Falhar($"Erro inesperado: {ex.Message}");
        }
    }

    public async Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
    {
        if (_registro is { Chave: string chave, InstanceId: string instanceId })
        {
            try
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("license_key", chave),
                    new KeyValuePair<string, string>("instance_id", instanceId),
                });
                await _http.PostAsync($"{ApiBase}/deactivate", form, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Falha ao notificar desativação ao LemonSqueezy (best-effort).");
            }
        }

        if (File.Exists(ArquivoLicenca))
            File.Delete(ArquivoLicenca);
        _registro = null;
        _tipoAtual = TipoLicenca.Gratuita;
        return ResultadoAtivacao.Ok(TipoLicenca.Gratuita);
    }

    public async Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default)
    {
        if (_registro is null || _tipoAtual != TipoLicenca.Premium)
            return ResultadoAtivacao.Ok(TipoLicenca.Gratuita);

        // Captura local: evita CS8602 pois _registro pode ser anulado dentro do try
        var reg = _registro;

        try
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("license_key", reg.Chave ?? ""),
                new KeyValuePair<string, string>("instance_id", reg.InstanceId ?? ""),
            });

            var resp = await _http.PostAsync($"{ApiBase}/validate", form, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool valido = root.TryGetProperty("valid", out var v) && v.GetBoolean();

            if (!valido)
            {
                _log.LogWarning("Assinatura expirada ou cancelada.");
                if (File.Exists(ArquivoLicenca)) File.Delete(ArquivoLicenca);
                _registro = null;
                _tipoAtual = TipoLicenca.Gratuita;
                return ResultadoAtivacao.Falhar("Assinatura expirada ou cancelada.");
            }

            var atualizado = reg with { UltimaValidacao = DateTimeOffset.UtcNow };
            await SalvarNoDisco(atualizado, ct);
            _registro = atualizado;
            return ResultadoAtivacao.Ok(TipoLicenca.Premium, atualizado.NomeCliente, atualizado.EmailCliente);
        }
        catch (Exception ex)
        {
            var diasOffline = (DateTimeOffset.UtcNow - reg.UltimaValidacao).TotalDays;
            _log.LogWarning(ex, "Sem acesso ao servidor; dias sem validação: {D:F0}.", diasOffline);

            if (diasOffline <= LicencaConfig.DiasGracePeriodo)
                return ResultadoAtivacao.Ok(TipoLicenca.Premium, reg.NomeCliente, reg.EmailCliente);

            _tipoAtual = TipoLicenca.Gratuita;
            return ResultadoAtivacao.Falhar(
                $"Sem acesso à internet por mais de {LicencaConfig.DiasGracePeriodo} dias. Licença temporariamente suspensa.");
        }
    }

    private void CarregarDoDisco()
    {
        if (!File.Exists(ArquivoLicenca)) return;
        try
        {
            var cifrado = File.ReadAllBytes(ArquivoLicenca);
            var json = DescifrarDpapi(cifrado);
            var registro = JsonSerializer.Deserialize<RegistroLicenca>(json);
            if (registro?.Tipo == TipoLicenca.Premium && !string.IsNullOrEmpty(registro.InstanceId))
            {
                _registro = registro;
                _tipoAtual = TipoLicenca.Premium;
            }
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

    public void Dispose() => _http.Dispose();

    private sealed record RegistroLicenca
    {
        public string? Chave { get; init; }
        public string? InstanceId { get; init; }
        public TipoLicenca Tipo { get; init; }
        public DateTimeOffset AtivoEm { get; init; }
        public DateTimeOffset UltimaValidacao { get; init; }
        public string? NomeCliente { get; init; }
        public string? EmailCliente { get; init; }
    }
}
