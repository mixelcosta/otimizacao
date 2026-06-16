using System.Diagnostics;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HardwareOptimizer.Agent.Startup;

/// <summary>
/// Varre as entradas de inicialização automática do Windows nos registros
/// HKCU/HKLM\Run (32 e 64 bits) e nas pastas de Startup. Operação somente leitura.
/// O status Ativo é determinado pela chave StartupApproved, que é o mecanismo
/// padrão do Gerenciador de Tarefas do Windows para habilitar/desabilitar entradas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VerificadorInicializacao
{
    private static readonly Dictionary<string, ImpactoInicializacao> _impactoConhecido =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "spotify", ImpactoInicializacao.Medio },
            { "discord", ImpactoInicializacao.Medio },
            { "steam", ImpactoInicializacao.Alto },
            { "onedrive", ImpactoInicializacao.Alto },
            { "teams", ImpactoInicializacao.Alto },
            { "skype", ImpactoInicializacao.Medio },
            { "slack", ImpactoInicializacao.Medio },
            { "zoom", ImpactoInicializacao.Medio },
            { "dropbox", ImpactoInicializacao.Medio },
            { "googledrive", ImpactoInicializacao.Medio },
            { "googleupdate", ImpactoInicializacao.Baixo },
            { "epicgames", ImpactoInicializacao.Alto },
            { "origin", ImpactoInicializacao.Alto },
            { "ea", ImpactoInicializacao.Alto },
            { "uplay", ImpactoInicializacao.Alto },
            { "battle", ImpactoInicializacao.Alto },
            { "razer", ImpactoInicializacao.Baixo },
            { "corsair", ImpactoInicializacao.Baixo },
            { "logitech", ImpactoInicializacao.Baixo },
            { "whatsapp", ImpactoInicializacao.Baixo },
            { "telegram", ImpactoInicializacao.Baixo },
            { "linkedin", ImpactoInicializacao.Baixo },
        };

    private readonly ILogger<VerificadorInicializacao> _log;

    public VerificadorInicializacao(ILogger<VerificadorInicializacao> log)
    {
        _log = log;
    }

    public IReadOnlyList<InicializacaoEntrada> Varrer()
    {
        var entradas = new List<InicializacaoEntrada>();

        // Lê o status habilitado/desabilitado de cada entrada via StartupApproved.
        // Este é o mecanismo padrão do Gerenciador de Tarefas — byte[0]=2 é habilitado,
        // byte[0]=3 é desabilitado. Sem entrada em StartupApproved = habilitado por padrão.
        var approvedHkcu = LerStartupApproved(
            Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
        var approvedHklm = LerStartupApproved(
            Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
        var approvedStartupFolder = LerStartupApproved(
            Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder");

        VarrerChaveRegistro(entradas,
            Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            OrigemInicializacao.RegistroUsuario,
            approvedHkcu);

        VarrerChaveRegistro(entradas,
            Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            OrigemInicializacao.RegistroMaquina,
            approvedHklm);

        // Apps 32-bit registram em WOW6432Node quando instalados em sistema 64-bit.
        // Abre explicitamente a visão 32-bit do registro para capturá-los.
        try
        {
            using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            VarrerChaveRegistro(entradas,
                hklm32,
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                OrigemInicializacao.RegistroMaquina,
                approvedHklm);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao varrer registro 32-bit.");
        }

        VarrerPastaStartup(entradas,
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            OrigemInicializacao.PastaStartup,
            approvedStartupFolder);

        VarrerPastaStartup(entradas,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            OrigemInicializacao.PastaStartup,
            approvedStartupFolder);

        // Deduplica por nome (32-bit e 64-bit podem registrar a mesma entrada).
        var deduplicadas = entradas
            .GroupBy(e => e.Nome, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Nome)
            .ToList();

        _log.LogInformation("Startup scan: {Qtd} entradas encontradas.", deduplicadas.Count);
        return deduplicadas;
    }

    // Lê a chave StartupApproved e retorna dicionário nome → ativo(true=habilitado).
    private static Dictionary<string, bool> LerStartupApproved(RegistryKey raiz, string subcaminho)
    {
        var resultado = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var chave = raiz.OpenSubKey(subcaminho, writable: false);
            if (chave is null) return resultado;

            foreach (var nome in chave.GetValueNames())
            {
                if (chave.GetValue(nome) is byte[] dados && dados.Length >= 4)
                {
                    // Primeiros 4 bytes formam um inteiro: 2=habilitado, 3=desabilitado.
                    resultado[nome] = dados[0] != 3;
                }
            }
        }
        catch { /* ignora — chave pode não existir */ }
        return resultado;
    }

    private void VarrerChaveRegistro(
        List<InicializacaoEntrada> destino,
        RegistryKey raiz,
        string subcaminho,
        OrigemInicializacao origem,
        Dictionary<string, bool> startupApproved)
    {
        try
        {
            using var chave = raiz.OpenSubKey(subcaminho, writable: false);
            if (chave is null) return;

            foreach (var nome in chave.GetValueNames())
            {
                var caminho = chave.GetValue(nome)?.ToString() ?? string.Empty;

                // Se existe em StartupApproved, usa esse status; senão assume habilitado.
                var ativo = !startupApproved.TryGetValue(nome, out var aprovado) || aprovado;

                destino.Add(new InicializacaoEntrada
                {
                    Nome          = nome,
                    Caminho       = caminho,
                    Impacto       = ClassificarImpacto(nome, caminho),
                    Origem        = origem,
                    Ativo         = ativo,
                    Fabricante    = LerFabricante(caminho),
                    ChaveRollback = $"{raiz.Name}\\{subcaminho}\\{nome}",
                });
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao varrer chave {Chave}.", subcaminho);
        }
    }

    private void VarrerPastaStartup(
        List<InicializacaoEntrada> destino,
        string pasta,
        OrigemInicializacao origem,
        Dictionary<string, bool> startupApproved)
    {
        if (!Directory.Exists(pasta)) return;

        try
        {
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.lnk"))
            {
                var nome = Path.GetFileNameWithoutExtension(arquivo);
                var renomeado = arquivo.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var ativo = !renomeado &&
                    (!startupApproved.TryGetValue(nome, out var aprovado) || aprovado);

                destino.Add(new InicializacaoEntrada
                {
                    Nome = nome,
                    Caminho = arquivo,
                    Impacto = ClassificarImpacto(nome, arquivo),
                    Origem = origem,
                    Ativo = ativo,
                    ChaveRollback = arquivo,
                });
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao varrer pasta {Pasta}.", pasta);
        }
    }

    private static string? LerFabricante(string caminho)
    {
        try
        {
            var exe = ExtrairExePath(caminho);
            if (exe is null || !File.Exists(exe)) return null;
            var company = FileVersionInfo.GetVersionInfo(exe).CompanyName?.Trim();
            return string.IsNullOrWhiteSpace(company) ? null : company;
        }
        catch { return null; }
    }

    private static string? ExtrairExePath(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            return end > 0 ? cmd[1..end] : null;
        }
        var sp = cmd.IndexOf(' ');
        return sp > 0 ? cmd[..sp] : cmd;
    }

    private static ImpactoInicializacao ClassificarImpacto(string nome, string caminho)
    {
        var chave = new[] { nome, caminho }
            .SelectMany(s => s.Split(new[] { ' ', '\\', '/', '.', '-', '_' }))
            .FirstOrDefault(token =>
                _impactoConhecido.ContainsKey(token) && token.Length > 2);

        return chave is not null && _impactoConhecido.TryGetValue(chave, out var impacto)
            ? impacto
            : ImpactoInicializacao.Desconhecido;
    }
}
