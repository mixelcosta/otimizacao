using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.EventLog;

/// <summary>
/// Leitor de eventos de instabilidade (BSOD/WHEA/crash de aplicação) do Event
/// Log do Windows via <see cref="EventLogReader"/> nativo do .NET — sem invocar
/// PowerShell (diferente do padrão dos demais leitores da plataforma, ver
/// Design Notes da spec-1-5-causa-raiz-event-log). Sempre sob demanda: só é
/// instanciado/chamado a partir de uma ação explícita do usuário, nunca em
/// timer/daemon (Boundaries §Always/§Never).
///
/// Cada consulta (WHEA/BSOD/crash) roda em seu próprio try/catch, retornando
/// lista vazia em caso de falha (canal indisponível, permissão, etc.) — mesmo
/// padrão defensivo de <c>LeitorWindows.Coletar*</c>. Nunca propaga exceção
/// para o chamador (I/O Matrix).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorEventLog : ILeitorEventLog
{
    private readonly ILogger _log;

    public LeitorEventLog(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public Task<IReadOnlyList<EventoInstabilidade>> LerAsync(
        int diasRecentes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo eventos de instabilidade do Event Log (últimos {Dias} dia(s)).", diasRecentes);

        var dias = Math.Max(1, diasRecentes);
        var eventos = new List<EventoInstabilidade>();

        eventos.AddRange(LerWhea(dias));
        eventos.AddRange(LerBsod(dias));
        eventos.AddRange(LerCrashAplicacao(dias));

        IReadOnlyList<EventoInstabilidade> resultado = eventos
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return Task.FromResult(resultado);
    }

    private IReadOnlyList<EventoInstabilidade> LerWhea(int dias)
    {
        try
        {
            // Level 1/2 (Crítico/Erro) — sem esse filtro, o provider WHEA-Logger
            // também loga eventos informativos/corrigidos que nunca causaram
            // instabilidade real, diluindo o sinal e podendo gerar "Causa
            // provável: BIOS desatualizada" pra eventos que não representam
            // travamento (achado da revisão independente da Story 1.5).
            return Consultar(
                "System",
                "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and (Level=1 or Level=2) " +
                $"and TimeCreated[timediff(@SystemTime) <= {LimiteMs(dias)}]]]",
                TipoEventoInstabilidade.Whea);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao ler eventos WHEA do Event Log.");
            return Array.Empty<EventoInstabilidade>();
        }
    }

    private IReadOnlyList<EventoInstabilidade> LerBsod(int dias)
    {
        try
        {
            return Consultar(
                "System",
                "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and (EventID=1001) " +
                $"and TimeCreated[timediff(@SystemTime) <= {LimiteMs(dias)}]]]",
                TipoEventoInstabilidade.Bsod);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao ler eventos de BSOD do Event Log.");
            return Array.Empty<EventoInstabilidade>();
        }
    }

    private IReadOnlyList<EventoInstabilidade> LerCrashAplicacao(int dias)
    {
        try
        {
            return Consultar(
                "Application",
                "*[System[Provider[@Name='Application Error'] and (EventID=1000) " +
                $"and TimeCreated[timediff(@SystemTime) <= {LimiteMs(dias)}]]]",
                TipoEventoInstabilidade.CrashAplicacao);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao ler eventos de crash de aplicação do Event Log.");
            return Array.Empty<EventoInstabilidade>();
        }
    }

    private static long LimiteMs(int dias) => dias * 24L * 60 * 60 * 1000;

    /// <summary>
    /// Máximo de eventos lidos por categoria (WHEA/BSOD/crash) — uma máquina com
    /// problema crônico de hardware pode gerar milhares de eventos WHEA em 30
    /// dias; sem cap, a leitura e a renderização em <c>ItemsControl</c> (sem
    /// virtualização) degradam justamente no cenário que a história mais precisa
    /// atender (achado da revisão independente da Story 1.5).
    /// </summary>
    private const int MaxEventosPorCategoria = 200;

    private static List<EventoInstabilidade> Consultar(string canal, string xpath, TipoEventoInstabilidade tipo)
    {
        var resultado = new List<EventoInstabilidade>();
        // ReverseDirection: sem isso, ReadEvent() devolve do mais antigo pro mais
        // recente — combinado com MaxEventosPorCategoria, o corte manteria os
        // eventos mais ANTIGOS da janela e descartaria os mais recentes, o
        // oposto do que a história promete numa máquina com muitos eventos
        // (achado da revisão independente da Story 1.5).
        var query = new EventLogQuery(canal, PathType.LogName, xpath) { ReverseDirection = true };

        using var reader = new EventLogReader(query);

        EventRecord? record;
        while (resultado.Count < MaxEventosPorCategoria && (record = reader.ReadEvent()) is not null)
        {
            using (record)
            {
                var timestamp = record.TimeCreated is { } tc
                    ? new DateTimeOffset(tc)
                    : DateTimeOffset.Now;

                var mensagem = SeguroOuNulo(() => record.FormatDescription());
                var origem = SeguroOuNulo(() => record.ProviderName) ?? canal;

                resultado.Add(new EventoInstabilidade
                {
                    Timestamp = timestamp,
                    Tipo = tipo,
                    Origem = origem,
                    ProcessoOuDriver = ExtratorEventoTexto.ExtrairProcessoOuDriver(tipo, mensagem),
                    Mensagem = mensagem,
                });
            }
        }

        return resultado;
    }

    private static T? SeguroOuNulo<T>(Func<T> acao)
        where T : class
    {
        try
        {
            return acao();
        }
        catch (EventLogException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
