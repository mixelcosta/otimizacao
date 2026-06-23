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
    [ObservableProperty] private string _nomeCliente = string.Empty;
    [ObservableProperty] private string _emailCliente = string.Empty;

    public string VersaoApp => "v1.0.0-beta";

    private void AtualizarStatus()
    {
        EPremium = _licenca.TipoAtual == TipoLicenca.Premium;
        StatusLicenca = EPremium
            ? "Premium — todos os módulos desbloqueados"
            : "Gratuita — módulos Premium bloqueados";
        NomeCliente = _licenca.NomeCliente ?? string.Empty;
        EmailCliente = _licenca.EmailCliente ?? string.Empty;
    }

    [RelayCommand(CanExecute = nameof(PodeAtivar))]
    private async Task AtivarAsync()
    {
        Ocupado = true;
        MensagemAtivacao = "Conectando ao servidor de licenças...";
        try
        {
            var resultado = await _licenca.AtivarAsync(ChaveAtivacao.Trim());
            if (resultado.Sucesso)
            {
                AtualizarStatus();
                _onLicencaAlterada();
                var saudacao = resultado.NomeCliente is { Length: > 0 } nome
                    ? $"Bem-vindo, {nome}! "
                    : string.Empty;
                MensagemAtivacao = $"{saudacao}Licença Premium ativada com sucesso.";
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

    [RelayCommand]
    private async Task ValidarOnlineAsync()
    {
        Ocupado = true;
        MensagemAtivacao = "Verificando licença online...";
        try
        {
            var resultado = await _licenca.ValidarOnlineAsync();
            if (resultado.Sucesso)
            {
                AtualizarStatus();
                _onLicencaAlterada();
                MensagemAtivacao = "Licença verificada e válida.";
            }
            else
            {
                AtualizarStatus();
                _onLicencaAlterada();
                MensagemAtivacao = resultado.Erro ?? "Licença inválida.";
            }
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void AbrirPaginaCompra()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = LicencaConfig.UrlCompra,
            UseShellExecute = true,
        });
    }

    partial void OnChaveAtivacaoChanged(string value) => AtivarCommand.NotifyCanExecuteChanged();
    partial void OnOcupadoChanged(bool value) => AtivarCommand.NotifyCanExecuteChanged();
}
