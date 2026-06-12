using System.Runtime.Versioning;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HardwareOptimizer.Agent.Startup;

/// <summary>
/// Aplica e reverte alterações de startup usando o mecanismo padrão do Windows:
/// a chave StartupApproved para entradas de registro, e renomeação para pastas.
/// A entrada permanece em Run — apenas o flag habilitado/desabilitado muda.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GerenciadorInicializacao
{
    // Formato do valor binário em StartupApproved: 12 bytes, byte[0] = status.
    // 2 = habilitado, 3 = desabilitado. Bytes restantes = zeros (timestamp reservado).
    private const byte StatusHabilitado = 2;
    private const byte StatusDesabilitado = 3;
    private const int TamanhoApprovedBytes = 12;

    private readonly ILogger<GerenciadorInicializacao> _log;

    public GerenciadorInicializacao(ILogger<GerenciadorInicializacao> log)
    {
        _log = log;
    }

    public Resultado Desativar(InicializacaoEntrada entrada)
    {
        try
        {
            return entrada.Origem switch
            {
                OrigemInicializacao.RegistroUsuario or OrigemInicializacao.RegistroMaquina =>
                    AlterarStatusRegistro(entrada, StatusDesabilitado),
                OrigemInicializacao.PastaStartup =>
                    DesativarArquivoStartup(entrada),
                _ => Resultado.Falhar("Origem de inicialização desconhecida."),
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao desativar entrada '{Nome}'.", entrada.Nome);
            return Resultado.Falhar(ex.Message);
        }
    }

    public Resultado Ativar(InicializacaoEntrada entrada, string valorOriginal)
    {
        try
        {
            return entrada.Origem switch
            {
                OrigemInicializacao.RegistroUsuario or OrigemInicializacao.RegistroMaquina =>
                    AlterarStatusRegistro(entrada, StatusHabilitado),
                OrigemInicializacao.PastaStartup =>
                    AtivarArquivoStartup(entrada),
                _ => Resultado.Falhar("Origem desconhecida."),
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao ativar entrada '{Nome}'.", entrada.Nome);
            return Resultado.Falhar(ex.Message);
        }
    }

    private Resultado AlterarStatusRegistro(InicializacaoEntrada entrada, byte status)
    {
        var raiz = entrada.Origem == OrigemInicializacao.RegistroUsuario
            ? Registry.CurrentUser
            : Registry.LocalMachine;

        const string subcaminhoApproved =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        using var chaveApproved = raiz.CreateSubKey(subcaminhoApproved, writable: true);
        if (chaveApproved is null)
            return Resultado.Falhar("Não foi possível acessar a chave StartupApproved.");

        var marcador = new byte[TamanhoApprovedBytes];
        marcador[0] = status;
        chaveApproved.SetValue(entrada.Nome, marcador, RegistryValueKind.Binary);

        var acao = status == StatusDesabilitado ? "desativada" : "reativada";
        _log.LogInformation("Entrada '{Nome}' {Acao} via StartupApproved.", entrada.Nome, acao);
        return Resultado.Ok();
    }

    private Resultado DesativarArquivoStartup(InicializacaoEntrada entrada)
    {
        if (!File.Exists(entrada.Caminho))
            return Resultado.Falhar($"Arquivo não encontrado: {entrada.Caminho}");

        var destino = entrada.Caminho + ".disabled";
        File.Move(entrada.Caminho, destino, overwrite: true);
        _log.LogInformation("Startup '{Nome}' desativado (renomeado).", entrada.Nome);
        return Resultado.Ok();
    }

    private Resultado AtivarArquivoStartup(InicializacaoEntrada entrada)
    {
        var origem = entrada.Caminho.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? entrada.Caminho
            : entrada.Caminho + ".disabled";

        if (!File.Exists(origem))
            return Resultado.Falhar($"Backup não encontrado: {origem}");

        var destino = origem.Replace(".disabled", string.Empty, StringComparison.OrdinalIgnoreCase);
        File.Move(origem, destino, overwrite: true);
        _log.LogInformation("Startup '{Nome}' reativado.", entrada.Nome);
        return Resultado.Ok();
    }
}
