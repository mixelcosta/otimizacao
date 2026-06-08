using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.App.ViewModels;

/// <summary>Item da matriz de decisão exibido na UI, com seleção para aprovação.</summary>
public partial class ItemMatrizViewModel : ObservableObject
{
    private readonly ItemDecisao _item;

    public ItemMatrizViewModel(ItemDecisao item)
    {
        _item = item;
        // Pré-seleciona apenas as ações de risco muito baixo (postura conservadora).
        _selecionado = item.Risco <= NivelRisco.MuitoBaixo;
    }

    [ObservableProperty]
    private bool _selecionado;

    public string AcaoId => _item.AcaoId;

    public string Descricao => $"{_item.Prioridade}. {_item.AcaoId} — {_item.Justificativa}";

    public string Risco => $"risco {_item.Risco}";
}
