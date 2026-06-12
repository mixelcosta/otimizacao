using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class IaCopilotoViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public IaCopilotoViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        Mensagens = new ObservableCollection<MensagemChatViewModel>();
    }

    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _inputUsuario = string.Empty;
    [ObservableProperty] private bool _temAlertaPendente;

    public ObservableCollection<MensagemChatViewModel> Mensagens { get; }

    /// <summary>Alerta enviado pelo Windows Service. Inserido no topo do chat.</summary>
    public void ReceberAlerta(string mensagem)
    {
        Mensagens.Insert(0, new MensagemChatViewModel(
            "Sistema", mensagem, DateTimeOffset.Now, EhAlerta: true));
        TemAlertaPendente = true;
    }

    public void MarcarComoLido() => TemAlertaPendente = false;

    [RelayCommand]
    private async Task EnviarAsync()
    {
        if (string.IsNullOrWhiteSpace(InputUsuario)) return;

        var texto = InputUsuario.Trim();
        InputUsuario = string.Empty;
        Mensagens.Add(new MensagemChatViewModel("Você", texto, DateTimeOffset.Now));

        Ocupado = true;
        try
        {
            var resp = await _agente.TratarAsync(new RequisicaoIpc
            {
                Metodo = "proposta",
                Parametros = JsonSerializer.SerializeToElement(new { pergunta = texto }),
            });

            var resposta = resp.Sucesso
                ? FormatarResposta(resp.Resultado)
                : "Erro: " + resp.Erro;

            Mensagens.Add(new MensagemChatViewModel("IA Copiloto", resposta, DateTimeOffset.Now));
        }
        finally { Ocupado = false; }
    }

    private static string FormatarResposta(object? resultado)
    {
        if (resultado is MatrizDecisao matriz)
        {
            if (matriz.Itens.Count == 0)
                return "Nenhuma otimização identificada para o hardware atual.";

            var sb = new StringBuilder();
            sb.AppendLine($"Encontrei {matriz.Itens.Count} otimização(ões) para o seu PC:\n");

            foreach (var item in matriz.Itens.OrderBy(i => i.Prioridade))
            {
                var risco = item.Risco switch
                {
                    HardwareOptimizer.Core.Common.NivelRisco.MuitoBaixo => "Muito Baixo",
                    HardwareOptimizer.Core.Common.NivelRisco.Baixo => "Baixo",
                    HardwareOptimizer.Core.Common.NivelRisco.Medio => "Médio",
                    HardwareOptimizer.Core.Common.NivelRisco.Alto => "Alto",
                    _ => "Nenhum",
                };
                sb.AppendLine($"• [{item.Categoria}] {item.AcaoId}");
                sb.AppendLine($"  Risco: {risco}  |  Prioridade: {item.Prioridade}");
                sb.AppendLine($"  {item.Justificativa}");
                if (!string.IsNullOrEmpty(item.GanhoEsperado))
                    sb.AppendLine($"  Ganho esperado: {item.GanhoEsperado}");
                sb.AppendLine();
            }

            if (matriz.Avisos.Count > 0)
            {
                sb.AppendLine("⚠ Avisos:");
                foreach (var aviso in matriz.Avisos)
                    sb.AppendLine($"  • {aviso}");
            }

            return sb.ToString().TrimEnd();
        }

        return resultado?.ToString() ?? "Sem resposta.";
    }
}

public sealed record MensagemChatViewModel(
    string Autor,
    string Texto,
    DateTimeOffset Hora,
    bool EhAlerta = false);
