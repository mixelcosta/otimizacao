using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Features.Atualizacao;

/// <summary>
/// Correlaciona eventos de instabilidade lidos do Event Log (<c>LeitorEventLog</c>
/// no Agent) com drivers/BIOS já sinalizados como desatualizados pelas Stories
/// 1.2/1.4 — classe pura, sem I/O, totalmente testável sem Windows.
///
/// Critério de "correlação plausível" (ver Design Notes da
/// spec-1-5-causa-raiz-event-log): (1) correspondência de fabricante — o texto
/// do evento (<see cref="EventoInstabilidade.Origem"/>/
/// <see cref="EventoInstabilidade.ProcessoOuDriver"/>/
/// <see cref="EventoInstabilidade.Mensagem"/>) contém o <see cref="InfoDriver.Fabricante"/>
/// de um driver já desatualizado; (2) heurística WHEA↔BIOS — evento
/// <see cref="TipoEventoInstabilidade.Whea"/> quando há BIOS desatualizada
/// sinalizada (<see cref="InfoBios"/> não-nulo). Fora desses dois casos,
/// <see cref="EventoInstabilidade.CausaProvavel"/> permanece nulo — nunca
/// inventar uma causa (guard anti-alucinação, mesma regra de FR1/NFR2 já usada
/// em <c>VerificadorSoftware</c>/<c>VerificadorBios</c>).
/// </summary>
public sealed class CorrelacionadorCausaRaiz
{
    /// <summary>
    /// Fabricantes genéricos demais pra servir de sinal de correlação — valores
    /// reais de <see cref="InfoDriver.Fabricante"/> pra drivers built-in/genéricos
    /// do Windows, mas que também aparecem como substring em praticamente
    /// qualquer <see cref="EventoInstabilidade.Origem"/> de primeira-parte (ex.
    /// "Microsoft-Windows-WHEA-Logger" contém "Microsoft") — sem essa exclusão,
    /// qualquer driver desatualizado com um desses fabricantes geraria
    /// falso-positivo de causa em quase todo evento WHEA, violando o guard
    /// anti-alucinação (achado da revisão independente da Story 1.5).
    /// </summary>
    private static readonly HashSet<string> FabricantesGenericosDemais = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Standard", "Generic", "(Standard system devices)",
    };

    public IReadOnlyList<EventoInstabilidade> Correlacionar(
        IReadOnlyList<EventoInstabilidade> eventos,
        IReadOnlyList<InfoDriver> driversDesatualizados,
        InfoBios? bios)
    {
        ArgumentNullException.ThrowIfNull(eventos);
        ArgumentNullException.ThrowIfNull(driversDesatualizados);

        var resultado = new List<EventoInstabilidade>(eventos.Count);

        foreach (var evento in eventos)
        {
            var causa = DeterminarCausa(evento, driversDesatualizados, bios);
            resultado.Add(causa is null ? evento : evento with { CausaProvavel = causa });
        }

        return resultado;
    }

    private static string? DeterminarCausa(
        EventoInstabilidade evento,
        IReadOnlyList<InfoDriver> driversDesatualizados,
        InfoBios? bios)
    {
        // Regra 1: correspondência de fabricante — texto do evento cita o
        // fabricante de um driver já sinalizado como desatualizado.
        var driver = driversDesatualizados.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(d.Fabricante)
            && !FabricantesGenericosDemais.Contains(d.Fabricante!)
            && MencionaFabricante(evento, d.Fabricante!));

        if (driver is not null)
            return driver.Descricao;

        // Regra 2: heurística WHEA↔BIOS — eventos WHEA são frequentemente
        // corrigidos por atualização de BIOS/AGESA já sinalizada.
        if (evento.Tipo == TipoEventoInstabilidade.Whea && bios is not null)
            return "BIOS desatualizada";

        return null; // sem correlação plausível — nunca inventar
    }

    private static bool MencionaFabricante(EventoInstabilidade evento, string fabricante) =>
        Contem(evento.Origem, fabricante) ||
        Contem(evento.ProcessoOuDriver, fabricante) ||
        Contem(evento.Mensagem, fabricante);

    private static bool Contem(string? texto, string termo) =>
        !string.IsNullOrWhiteSpace(texto) && texto.Contains(termo, StringComparison.OrdinalIgnoreCase);
}
