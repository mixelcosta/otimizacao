namespace HardwareOptimizer.Core.Contracts;

public enum TipoEventoInstabilidade
{
    Bsod = 0,
    Whea = 1,
    CrashAplicacao = 2,
}

/// <summary>
/// Evento de instabilidade lido do Event Log do Windows (spec-1-5), sob demanda
/// (nunca em timer/daemon/background). <see cref="CausaProvavel"/> só é
/// preenchido pela correlação de <c>CorrelacionadorCausaRaiz</c> quando há
/// correlação plausível com um driver/BIOS já sinalizado como desatualizado
/// (Stories 1.2/1.4) — nunca inventado (guard anti-alucinação, mesma regra de
/// FR1/NFR2 já usada em <see cref="InfoSoftware"/>/<see cref="InfoBios"/>).
/// </summary>
public sealed record EventoInstabilidade
{
    public required DateTimeOffset Timestamp { get; init; }

    public required TipoEventoInstabilidade Tipo { get; init; }

    /// <summary>Origem do evento (ex.: <c>Provider.Name</c> do Event Log).</summary>
    public required string Origem { get; init; }

    /// <summary>
    /// Processo ou driver associado, extraído do próprio evento quando presente
    /// (ex.: nome do módulo em falha num crash de aplicação). Nulo quando o
    /// evento não traz essa informação — nunca inferido.
    /// </summary>
    public string? ProcessoOuDriver { get; init; }

    public string? Mensagem { get; init; }

    /// <summary>Só preenchido pela correlação — ver <see cref="ProcessoOuDriver"/>.</summary>
    public string? CausaProvavel { get; init; }
}
