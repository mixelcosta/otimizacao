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
    }
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
