using System.Text.RegularExpressions;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.EventLog;

/// <summary>
/// Extração de processo/driver a partir do texto formatado de um evento do
/// Event Log — lógica pura de string/regex, sem nenhuma dependência do Windows
/// (por isso não vive em <see cref="LeitorEventLog"/>, que é
/// <c>[SupportedOSPlatform("windows")]</c> — o analisador CA1416 propagaria essa
/// exigência de plataforma pra um método que não precisa dela, achado da
/// revisão independente da Story 1.5).
/// </summary>
public static class ExtratorEventoTexto
{
    /// <summary>
    /// Extrai o processo/driver associado quando o próprio evento traz essa
    /// informação de forma estruturada (Boundaries §Always da spec-1-5) — nunca
    /// inferido a partir de heurística genérica. WHEA não tem campo estruturado
    /// de driver nos formatos de mensagem padrão; permanece nulo (a correlação
    /// usa a heurística WHEA↔BIOS, não este campo).
    ///
    /// A descrição formatada de um evento (<c>EventRecord.FormatDescription()</c>)
    /// é localizada conforme o idioma de exibição do Windows — os padrões em
    /// inglês (rótulos originais do Windows Error Reporting) e em português
    /// (idioma-alvo do produto) são tentados em sequência (achado da revisão
    /// independente da Story 1.5).
    /// </summary>
    public static string? ExtrairProcessoOuDriver(TipoEventoInstabilidade tipo, string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem)) return null;

        return tipo switch
        {
            TipoEventoInstabilidade.CrashAplicacao =>
                ExtrairViaRegex(mensagem, @"Faulting module name:\s*([^,\r\n]+)")
                ?? ExtrairViaRegex(mensagem, @"Faulting application name:\s*([^,\r\n]+)")
                ?? ExtrairViaRegex(mensagem, @"Nome do módulo com falha:\s*([^,\r\n]+)")
                ?? ExtrairViaRegex(mensagem, @"Nome do aplicativo com falha:\s*([^,\r\n]+)"),
            TipoEventoInstabilidade.Bsod =>
                ExtrairViaRegex(mensagem, @"([A-Za-z0-9_\-]+\.sys)"),
            _ => null,
        };
    }

    private static string? ExtrairViaRegex(string texto, string padrao)
    {
        var m = Regex.Match(texto, padrao, RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
