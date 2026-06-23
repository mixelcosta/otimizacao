using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Upgrade;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class UpgradeViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;
    private readonly CalculadoraGargalo _calc = new();

    public UpgradeViewModel(IRoteadorIpc agente) => _agente = agente;

    // ── State ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    private bool _carregando;

    [ObservableProperty] private bool _carregado;

    public string TextoBotao => Carregando ? "Detectando..." : "  Detectar Hardware  ";

    // ── Parts list ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _nomeCpu        = "–";
    [ObservableProperty] private string _infosCpu       = "";
    [ObservableProperty] private string _nomeGpu        = "–";
    [ObservableProperty] private string _infosGpu       = "";
    [ObservableProperty] private string _nomeRam        = "–";
    [ObservableProperty] private string _fabricanteRam  = "";
    [ObservableProperty] private string _infosRam       = "";
    [ObservableProperty] private string _nomePlacaMae   = "–";
    [ObservableProperty] private string _infosPlaca     = "";
    [ObservableProperty] private string _nomeOs         = "–";

    // ── Bottleneck ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGargalo))]
    private string _componenteLimitante = "";

    [ObservableProperty] private string _gargaloLabel      = "";
    [ObservableProperty] private string _gargaloDescricao  = "";

    // ── Sugestões concretas ────────────────────────────────────────────────
    [ObservableProperty] private bool   _temSugestoes;
    [ObservableProperty] private string _sugestaoTitulo   = "";
    [ObservableProperty] private string _sugestaoImpacto  = "";
    public ObservableCollection<SugestaoUpgradeVm> Sugestoes { get; } = [];

    public bool IsGargalo =>
        ComponenteLimitante.Equals("CPU", StringComparison.OrdinalIgnoreCase) ||
        ComponenteLimitante.Equals("GPU", StringComparison.OrdinalIgnoreCase);

    // ── Diagram visibility ─────────────────────────────────────────────────

    [ObservableProperty] private bool _temCpu;
    [ObservableProperty] private bool _temGpu;
    [ObservableProperty] private bool _temRam;
    [ObservableProperty] private bool _temPlaca;

    // ── Chat ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEnviar))]
    private string _mensagemInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEnviar))]
    private bool _chatCarregando;

    [ObservableProperty] private bool _chatVisivel;

    public bool PodeEnviar => !ChatCarregando && !string.IsNullOrWhiteSpace(MensagemInput);

    public ObservableCollection<ChatMensagemVm> Mensagens { get; } = [];

    // ── Commands ────────────────────────────────────────────────────────────

    public async Task AtivarAsync()
    {
        if (!Carregado) await CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (Carregando) return;
        Carregando = true;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "coletar" });
            if (!resp.Sucesso || resp.Resultado is not Inventario inv) return;

            PopularCpu(inv);
            PopularGpu(inv);
            PopularRam(inv);
            PopularPlaca(inv);

            NomeOs = inv.SistemaOperacional.Nome ?? inv.SistemaOperacional.Tipo.ToString();

            AnalisarGargalo(inv);

            Carregado = true;
            ChatVisivel = true;

            // Dispara análise automática da IA após detectar hardware
            _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                await AnalisarInicialAsync();
            });
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PodeEnviar))]
    private async Task EnviarMensagemAsync()
    {
        var texto = MensagemInput.Trim();
        if (string.IsNullOrWhiteSpace(texto)) return;

        MensagemInput = "";
        AdicionarMensagem("user", texto);

        ChatCarregando = true;
        try
        {
            var historico = Mensagens
                .Select(m => new { role = m.IsUser ? "user" : "assistant", conteudo = m.Texto })
                .ToList();

            // Remove a última mensagem do usuário do histórico (ela é a atual)
            var historicoSemAtual = historico.Take(historico.Count - 1).ToList();

            var parametros = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                mensagem = texto,
                historico = historicoSemAtual,
            })).RootElement;

            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo = "chat_upgrade",
                Parametros = parametros,
            });

            var resposta = resp.Sucesso && resp.Resultado is string s ? s
                : resp.Sucesso ? resp.Resultado?.ToString() ?? "–"
                : $"Erro: {resp.Erro}";

            AdicionarMensagem("assistant", resposta);
        }
        finally
        {
            ChatCarregando = false;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task AnalisarInicialAsync()
    {
        ChatCarregando = true;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc { Metodo = "analise_upgrade" });
            var texto = resp.Sucesso && resp.Resultado is string s ? s
                : resp.Sucesso ? resp.Resultado?.ToString() ?? "–"
                : null;

            if (!string.IsNullOrWhiteSpace(texto))
                AdicionarMensagem("assistant", texto);
        }
        catch
        {
            // análise automática falha silenciosamente
        }
        finally
        {
            ChatCarregando = false;
        }
    }

    private void AdicionarMensagem(string role, string texto) =>
        Mensagens.Add(new ChatMensagemVm(role, texto));

    private void PopularCpu(Inventario inv)
    {
        NomeCpu  = inv.Cpu.Nome;
        InfosCpu = inv.Cpu.Nucleos.HasValue
            ? $"{inv.Cpu.Nucleos} núcleos · {inv.Cpu.Threads} threads"
            : "";
        TemCpu = true;
    }

    private void PopularGpu(Inventario inv)
    {
        if (inv.Gpu.Count == 0) return;
        var gpu   = inv.Gpu[0];
        NomeGpu   = gpu.Nome;
        InfosGpu  = gpu.VersaoDriver is { } drv ? $"Driver {drv}" : "";
        TemGpu    = true;
    }

    private void PopularRam(Inventario inv)
    {
        if (inv.Memoria.Count == 0) return;

        var totalGb  = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
        if (totalGb == 0) return;

        var freqMhz  = inv.Memoria.FirstOrDefault()?.VelocidadeMhz;
        var qtd      = inv.Memoria.Count;
        var perStick = qtd > 0 ? totalGb / qtd : 0;

        var tipo = inv.Memoria.FirstOrDefault(m => m.Tipo != null)?.Tipo
                   ?? (freqMhz >= 4800 ? "DDR5" : "DDR4");

        NomeRam = freqMhz > 0
            ? $"{totalGb} GB {tipo}-{freqMhz}  ({qtd}×{perStick} GB)"
            : $"{totalGb} GB {tipo}  ({qtd}×{perStick} GB)";

        var fabricantes = inv.Memoria
            .Select(m => m.Fabricante)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .ToList();

        var modelos = inv.Memoria
            .Select(m => m.Modelo?.Trim())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .Take(2)
            .ToList();

        FabricanteRam = fabricantes.Count > 0 ? string.Join(" / ", fabricantes!) : "";

        var slots = inv.Memoria
            .Select(m => m.Slot)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var partes = new List<string>();
        if (modelos.Count > 0) partes.Add(string.Join(", ", modelos!));
        if (slots.Count > 0)   partes.Add(string.Join(", ", slots!));
        InfosRam = partes.Count > 0 ? string.Join("  ·  ", partes) : "";

        TemRam = true;
    }

    private void PopularPlaca(Inventario inv)
    {
        NomePlacaMae = $"{inv.Placa.Fabricante} {inv.Placa.Modelo}";
        InfosPlaca   = inv.Placa.VersaoBios is { } bios ? $"BIOS {bios}" : "";
        TemPlaca     = true;
    }

    private void AnalisarGargalo(Inventario inv)
    {
        var g = _calc.Calcular(inv);
        ComponenteLimitante = g.ComponenteLimitante;
        GargaloDescricao    = g.Descricao;
        GargaloLabel = g.ComponenteLimitante switch
        {
            "CPU" => $"⚠  Gargalo: CPU  (+{g.GanhoEstimadoPercent:F0}% estimado)",
            "GPU" => $"⚠  Gargalo: GPU  (+{g.GanhoEstimadoPercent:F0}% estimado)",
            _     => "✓  Setup Balanceado",
        };

        GerarSugestoes(inv, g);
    }

    private void GerarSugestoes(Inventario inv, GargaloResult g)
    {
        Sugestoes.Clear();

        switch (g.ComponenteLimitante)
        {
            case "CPU":
                SugestaoTitulo  = "Upgrade de CPU Recomendado";
                SugestaoImpacto = $"+{g.GanhoEstimadoPercent:F0}% de ganho estimado";
                foreach (var s in SugestoesCpu(inv))
                    Sugestoes.Add(s);
                break;

            case "GPU":
                SugestaoTitulo  = "Upgrade de GPU Recomendado";
                SugestaoImpacto = $"+{g.GanhoEstimadoPercent:F0}% de ganho estimado";
                foreach (var s in SugestoesGpu(inv))
                    Sugestoes.Add(s);
                break;

            default:
                SugestaoTitulo  = "Melhorias de Custo-Benefício";
                SugestaoImpacto = "Setup balanceado — melhorias incrementais disponíveis";
                foreach (var s in SugestoesBalanceadas(inv))
                    Sugestoes.Add(s);
                break;
        }

        TemSugestoes = Sugestoes.Count > 0;
    }

    private static IEnumerable<SugestaoUpgradeVm> SugestoesCpu(Inventario inv)
    {
        var soquete = inv.Cpu.Soquete ?? "";
        var nomeCpu = inv.Cpu.Nome;

        // AM5 socket → Ryzen 7000/9000
        if (soquete.Contains("AM5", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("Ryzen 7 7700X", "CPU", "AMD", "8C/16T · 4.5–5.4 GHz · AM5 · 105W", "Upgrade direto no socket atual — excelente custo-benefício");
            yield return new("Ryzen 9 7900X", "CPU", "AMD", "12C/24T · 4.7–5.6 GHz · AM5 · 170W", "Alta performance para produtividade + gaming");
            yield return new("Ryzen 9 9900X", "CPU", "AMD", "12C/24T · Zen 5 · AM5 · 120W", "Geração mais recente — melhor eficiência");
        }
        // AM4 socket → Ryzen 5000
        else if (soquete.Contains("AM4", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("Ryzen 7 5700X", "CPU", "AMD", "8C/16T · 3.4–4.6 GHz · AM4 · 65W", "Drop-in upgrade — sem trocar placa-mãe");
            yield return new("Ryzen 9 5900X", "CPU", "AMD", "12C/24T · 3.7–4.8 GHz · AM4 · 105W", "Melhor Ryzen para AM4 em uso geral + streaming");
            if (!nomeCpu.Contains("5800X3D", StringComparison.OrdinalIgnoreCase))
                yield return new("Ryzen 7 5800X3D", "CPU", "AMD", "8C/16T · 3D V-Cache · AM4 · 105W", "Melhor para gaming no socket AM4 — recomendado");
        }
        // LGA1700 → Intel 12th/13th/14th gen
        else if (soquete.Contains("1700", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("Core i5-13400F", "CPU", "Intel", "6P+4E · 2.5–4.6 GHz · LGA1700 · 65W", "Custo-benefício — drop-in upgrade");
            yield return new("Core i7-13700K", "CPU", "Intel", "8P+8E · 3.4–5.4 GHz · LGA1700 · 125W", "Alta performance gaming + multitarefa");
            yield return new("Core i9-14900K", "CPU", "Intel", "8P+16E · 3.2–6.0 GHz · LGA1700 · 125W", "Melhor desempenho disponível para LGA1700");
        }
        else
        {
            yield return new("Verificar CPUs compatíveis", "CPU", "–", $"Socket: {(string.IsNullOrEmpty(soquete) ? "não detectado" : soquete)}", "Consulte o manual da placa-mãe para CPUs suportadas");
        }
    }

    private static IEnumerable<SugestaoUpgradeVm> SugestoesGpu(Inventario inv)
    {
        var nomeGpu = inv.Gpu.FirstOrDefault()?.Nome ?? "";

        // RTX 4000 series — sugerir upgrade conservador
        if (nomeGpu.Contains("RTX 40", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("RTX 5080", "GPU", "NVIDIA", "16 GB GDDR7 · PCIe 5.0 · 360W", "Geração Blackwell — ganho máximo disponível");
            yield return new("RTX 5090", "GPU", "NVIDIA", "32 GB GDDR7 · PCIe 5.0 · 575W", "Melhor GPU do mercado · Verifique a fonte de alimentação");
        }
        // RTX 3000 series
        else if (nomeGpu.Contains("RTX 30", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("RTX 4070", "GPU", "NVIDIA", "12 GB GDDR6X · PCIe 4.0 · 200W", "Melhor custo-benefício para upgrade da RTX 30xx");
            yield return new("RTX 4070 Ti Super", "GPU", "NVIDIA", "16 GB GDDR6X · PCIe 4.0 · 285W", "Alta performance 1440p / 4K");
            yield return new("RTX 4080 Super", "GPU", "NVIDIA", "16 GB GDDR6X · PCIe 4.0 · 320W", "Performance de topo na série Ada Lovelace");
        }
        // RX 6000/7000 series AMD
        else if (nomeGpu.Contains("RX 6", StringComparison.OrdinalIgnoreCase) || nomeGpu.Contains("RX 7", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("RX 7800 XT", "GPU", "AMD", "16 GB GDDR6 · PCIe 4.0 · 263W", "Excelente custo-benefício 1440p");
            yield return new("RX 7900 GRE", "GPU", "AMD", "16 GB GDDR6 · PCIe 4.0 · 260W", "Alta performance 1440p/4K · Boa relação preço/desempenho");
            yield return new("RX 7900 XTX", "GPU", "AMD", "24 GB GDDR6 · PCIe 4.0 · 355W", "Melhor GPU AMD disponível");
        }
        else
        {
            yield return new("RTX 4060 Ti", "GPU", "NVIDIA", "16 GB GDDR6 · PCIe 4.0 · 165W", "Upgrade moderno de excelente custo-benefício");
            yield return new("RX 7700 XT", "GPU", "AMD", "12 GB GDDR6 · PCIe 4.0 · 245W", "Alternativa AMD de alto desempenho 1080p/1440p");
        }
    }

    private static IEnumerable<SugestaoUpgradeVm> SugestoesBalanceadas(Inventario inv)
    {
        var totalRam = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
        var qtdPentes = inv.Memoria.Count;

        // RAM: Single-channel → Dual-channel
        if (qtdPentes == 1)
            yield return new("Adicionar 2º pente de RAM", "RAM", "–", $"Total atual: {totalRam} GB em 1 slot (Single-Channel)", "Habilitar Dual-Channel — ganho de ~15% em memória sem trocar nada");

        // RAM: 16 GB → 32 GB
        if (totalRam <= 16)
            yield return new($"Upgrade para 32 GB RAM", "RAM", "–", $"Atual: {totalRam} GB · Adicionar módulos compatíveis", "32 GB é o novo padrão para gaming + produtividade simultâneos");

        // SSD NVMe
        if (inv.Metricas?.Discos?.Any(d => d.UsoPercent >= 80) == true)
            yield return new("SSD NVMe PCIe 4.0 / 5.0", "Storage", "–", "1 TB ou 2 TB · Sequencial > 7000 MB/s", "Partição de sistema acima de 80% — impacta desempenho geral");

        yield return new("Pasta térmica de alta performance", "Cooling", "–", "Noctua NT-H2 / Thermal Grizzly Kryonaut", "Custo mínimo — reduz temperatura da CPU em 5–15°C");
    }
}

/// <summary>Sugestão concreta de upgrade exibida na UI.</summary>
public sealed class SugestaoUpgradeVm
{
    public SugestaoUpgradeVm(string nome, string categoria, string fabricante, string specs, string motivo)
    {
        Nome       = nome;
        Categoria  = categoria;
        Fabricante = fabricante;
        Specs      = specs;
        Motivo     = motivo;
        CorCategoria = categoria switch
        {
            "CPU"     => "#00C8FF",
            "GPU"     => "#A060FF",
            "RAM"     => "#00C870",
            "Storage" => "#FFAA00",
            "Cooling" => "#FF6060",
            _         => "#484865",
        };
    }

    public string Nome        { get; }
    public string Categoria   { get; }
    public string Fabricante  { get; }
    public string Specs       { get; }
    public string Motivo      { get; }
    public string CorCategoria { get; }
}

/// <summary>Item de mensagem no chat de upgrade.</summary>
public sealed class ChatMensagemVm
{
    public ChatMensagemVm(string role, string texto)
    {
        Role = role;
        Texto = texto;
        IsUser = role == "user";
    }

    public string Role { get; }
    public string Texto { get; }
    public bool IsUser { get; }
    public bool IsAssistant => !IsUser;
}
