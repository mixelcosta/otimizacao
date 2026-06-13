using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Upgrade.Repositorio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Features.Upgrade.Agente;

/// <summary>
/// Agente de IA para sugestões de upgrade de hardware.
/// Usa o Claude para analisar o inventário atual e responder perguntas
/// sobre compatibilidade e ganhos estimados de performance.
/// Busca specs reais no banco de dados BuildCores (GitHub) quando necessário.
/// </summary>
public sealed class AgenteUpgrade
{
    private readonly IClienteLlm _llm;
    private readonly RepositorioBuildCores _db;
    private readonly ILogger _log;

    private const string SystemPrompt = """
        Você é um especialista em hardware de computadores integrado ao HardwareOptimizer.
        Seu papel é analisar o hardware atual do usuário e sugerir upgrades, verificar compatibilidade
        e estimar ganhos de performance.

        REGRAS IMPORTANTES:
        1. Responda SEMPRE em português do Brasil.
        2. Seja conciso e direto — use listas e formatação Markdown quando ajudar na leitura.
        3. Para cada sugestão de upgrade, informe:
           - Compatibilidade: SIM ou NÃO com justificativa técnica
           - Ganho estimado de performance (%)
           - Possíveis restrições (socket, DDR, PCIe, wattage da fonte)
        4. Se o upgrade NÃO for compatível, explique claramente o motivo técnico e sugira alternativas.
        5. Quando o usuário perguntar sobre uma peça específica, use os dados técnicos fornecidos no contexto.
        6. Nunca invente especificações técnicas — se não tiver certeza, informe.

        CONHECIMENTO DE COMPATIBILIDADE:
        - CPU: precisa do mesmo socket da placa-mãe
        - RAM DDR4 ≠ DDR5 — não são intercambiáveis
        - GPU: verifica slot PCIe e consumo estimado vs capacidade da fonte
        - M.2 NVMe PCIe 4.0 funciona em slot PCIe 3.0 (mais lento), mas não o inverso para PCIe 5.0
        - Dual-channel requer 2 pentes iguais no mesmo kit
        """;

    public AgenteUpgrade(IClienteLlm llm, ILogger? logger = null)
    {
        _llm = llm;
        _db = new RepositorioBuildCores(logger);
        _log = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Responde à mensagem do usuário com base no inventário atual e histórico de conversa.
    /// Busca automaticamente no BuildCores DB quando o usuário menciona um componente específico.
    /// </summary>
    public async Task<string> ResponderAsync(
        Inventario inventario,
        string mensagem,
        IReadOnlyList<MensagemChat> historico,
        CancellationToken cancellationToken = default)
    {
        var contextoDados = await BuscarContextoBuildCoresAsync(mensagem, cancellationToken);
        var sistema = MontarSistema(inventario, contextoDados);

        var conv = new List<(string Role, string Conteudo)>();
        foreach (var m in historico)
            conv.Add((m.Role, m.Conteudo));
        conv.Add(("user", mensagem));

        _log.LogInformation("AgenteUpgrade: enviando conversa ({Turnos} turnos) ao Claude.", conv.Count);
        return await _llm.ResponderConversaAsync(sistema, conv, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gera a análise inicial automática ao abrir a tela (sem mensagem do usuário).
    /// </summary>
    public async Task<string> AnalisarInicialAsync(
        Inventario inventario,
        CancellationToken cancellationToken = default)
    {
        var sistema = MontarSistema(inventario, null);
        const string Pergunta = """
            Analise o hardware deste computador e:
            1. Identifique o principal gargalo de performance (CPU, GPU ou RAM)
            2. Sugira os 2 upgrades mais custo-benefício para o setup atual
            3. Para cada upgrade: confirme compatibilidade e estime o ganho de performance
            Seja direto e use listas. Máximo de 200 palavras.
            """;

        var conv = new List<(string Role, string Conteudo)> { ("user", Pergunta) };
        return await _llm.ResponderConversaAsync(sistema, conv, cancellationToken).ConfigureAwait(false);
    }

    // ── Privado ────────────────────────────────────────────────────────────

    private static string MontarSistema(Inventario inv, string? contextoDados)
    {
        var ram = inv.Memoria.Count > 0
            ? $"{inv.Memoria.Sum(m => m.TamanhoGb ?? 0)} GB " +
              $"{inv.Memoria.FirstOrDefault(m => m.Tipo != null)?.Tipo ?? "DDR4"}-" +
              $"{inv.Memoria.FirstOrDefault()?.VelocidadeMhz ?? 0} MHz " +
              $"({inv.Memoria.Count}× módulos)"
            : "Não detectada";

        var ramFabricante = inv.Memoria
            .Select(m => m.Fabricante)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .FirstOrDefault() ?? "–";

        var gpu = inv.Gpu.Count > 0 ? inv.Gpu[0].Nome : "Não detectada";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("═══ HARDWARE ATUAL DO USUÁRIO ═══");
        sb.AppendLine($"CPU:       {inv.Cpu.Nome}" +
                      (inv.Cpu.Nucleos.HasValue ? $" ({inv.Cpu.Nucleos} cores/{inv.Cpu.Threads} threads)" : ""));
        sb.AppendLine($"GPU:       {gpu}");
        sb.AppendLine($"RAM:       {ram}  | Fabricante: {ramFabricante}");
        sb.AppendLine($"Placa-mãe: {inv.Placa.Fabricante} {inv.Placa.Modelo}");
        sb.AppendLine($"BIOS:      {inv.Placa.VersaoBios ?? "–"}");
        sb.AppendLine($"SO:        {inv.SistemaOperacional.Nome ?? inv.SistemaOperacional.Tipo.ToString()}");

        if (!string.IsNullOrWhiteSpace(contextoDados))
        {
            sb.AppendLine();
            sb.AppendLine("═══ DADOS DO BANCO BuildCores (componente pesquisado) ═══");
            sb.AppendLine(contextoDados);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detecta se o usuário menciona um componente específico e busca no BuildCores DB.
    /// Retorna as specs formatadas para incluir no contexto do Claude.
    /// </summary>
    private async Task<string?> BuscarContextoBuildCoresAsync(string mensagem, CancellationToken ct)
    {
        var categoria = DetectarCategoria(mensagem);
        if (categoria is null) return null;

        try
        {
            var specs = await _db.PesquisarAsync(categoria, mensagem, ct).ConfigureAwait(false);
            if (specs is null) return null;

            _log.LogInformation("AgenteUpgrade: dados BuildCores encontrados para '{Categoria}'.", categoria);
            return specs;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AgenteUpgrade: falha ao buscar BuildCores DB — continuando sem dados extras.");
            return null;
        }
    }

    private static string? DetectarCategoria(string msg)
    {
        var m = msg.ToUpperInvariant();

        if (m.Contains("CPU") || m.Contains("PROCESSADOR") || m.Contains("RYZEN") ||
            m.Contains("CORE I") || m.Contains("I5") || m.Contains("I7") || m.Contains("I9"))
            return "CPU";

        if (m.Contains("GPU") || m.Contains("PLACA DE VIDEO") || m.Contains("PLACA DE VÍDEO") ||
            m.Contains("RTX") || m.Contains("GTX") || m.Contains("RX ") || m.Contains("RADEON"))
            return "GPU";

        if (m.Contains("RAM") || m.Contains("MEMÓRIA") || m.Contains("MEMORIA") ||
            m.Contains("DDR") || m.Contains("PENTE"))
            return "RAM";

        if (m.Contains("SSD") || m.Contains("HD") || m.Contains("HDD") ||
            m.Contains("NVME") || m.Contains("M.2") || m.Contains("ARMAZENAMENTO"))
            return "Storage";

        if (m.Contains("FONTE") || m.Contains("PSU") || m.Contains("WATT"))
            return "PSU";

        if (m.Contains("PLACA MÃE") || m.Contains("PLACA-MÃE") || m.Contains("MOTHERBOARD"))
            return "Motherboard";

        return null;
    }
}
