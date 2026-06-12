using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Features.Licensing;

namespace HardwareOptimizer.App.ViewModels;

public partial class ConfiguracoesViewModel : ObservableObject
{
    private readonly IServicoLicenca _licenca;
    private readonly Action _onLicencaAlterada;

    public ConfiguracoesViewModel(IServicoLicenca licenca, Action onLicencaAlterada)
    {
        _licenca = licenca;
        _onLicencaAlterada = onLicencaAlterada;
        AtualizarStatus();
    }

    [ObservableProperty] private string _chaveAtivacao = string.Empty;
    [ObservableProperty] private string _statusLicenca = string.Empty;
    [ObservableProperty] private string _mensagemAtivacao = string.Empty;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private bool _ePremium;

    public string VersaoApp => "v1.0.0-beta";

    private void AtualizarStatus()
    {
        EPremium = _licenca.TipoAtual == TipoLicenca.Premium;
        StatusLicenca = EPremium ? "Premium — todos os módulos desbloqueados" : "Gratuita — módulos Premium bloqueados";
    }

    [RelayCommand(CanExecute = nameof(PodeAtivar))]
    private async Task AtivarAsync()
    {
        Ocupado = true;
        MensagemAtivacao = string.Empty;
        try
        {
            var resultado = await _licenca.AtivarAsync(ChaveAtivacao.Trim());
            if (resultado.Sucesso)
            {
                AtualizarStatus();
                _onLicencaAlterada();
                MensagemAtivacao = "Licença Premium ativada com sucesso.";
                ChaveAtivacao = string.Empty;
            }
            else
            {
                MensagemAtivacao = resultado.Erro ?? "Falha na ativação.";
            }
        }
        finally
        {
            Ocupado = false;
        }
    }

    private bool PodeAtivar() => !Ocupado && !string.IsNullOrWhiteSpace(ChaveAtivacao);

    [RelayCommand]
    private async Task DesativarAsync()
    {
        Ocupado = true;
        MensagemAtivacao = string.Empty;
        try
        {
            await _licenca.DesativarAsync();
            AtualizarStatus();
            _onLicencaAlterada();
            MensagemAtivacao = "Licença revertida para versão Gratuita.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    partial void OnChaveAtivacaoChanged(string value) => AtivarCommand.NotifyCanExecuteChanged();
    partial void OnOcupadoChanged(bool value) => AtivarCommand.NotifyCanExecuteChanged();
}
