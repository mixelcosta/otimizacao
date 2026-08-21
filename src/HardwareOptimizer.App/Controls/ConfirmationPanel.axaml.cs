using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace HardwareOptimizer.App.Controls;

/// <summary>Severidade do <see cref="ConfirmationPanel"/> — controla a cor de destaque da borda.</summary>
public enum SeveridadeConfirmacao
{
    Driver,
    Bios,
    Manutencao,
}

/// <summary>
/// Painel de confirmação inline, genérico, parametrizado por severidade. Nunca um
/// modal/popup — sempre inline no fluxo de scroll (convenção já estabelecida no app).
/// O botão de aplicar fica desabilitado até o usuário marcar a confirmação de risco.
/// </summary>
public partial class ConfirmationPanel : UserControl
{
    public static readonly StyledProperty<SeveridadeConfirmacao> SeveridadeProperty =
        AvaloniaProperty.Register<ConfirmationPanel, SeveridadeConfirmacao>(nameof(Severidade), SeveridadeConfirmacao.Driver);

    public static readonly StyledProperty<string> MensagemProperty =
        AvaloniaProperty.Register<ConfirmationPanel, string>(nameof(Mensagem), string.Empty);

    public static readonly StyledProperty<bool> PodeConfirmarProperty =
        AvaloniaProperty.Register<ConfirmationPanel, bool>(
            nameof(PodeConfirmar), defaultValue: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> ConfirmarCommandProperty =
        AvaloniaProperty.Register<ConfirmationPanel, ICommand?>(nameof(ConfirmarCommand));

    private static readonly IBrush CorAviso = new SolidColorBrush(Color.Parse("#FFAA00")); // status-warning
    private static readonly IBrush CorRisco = new SolidColorBrush(Color.Parse("#CC3333")); // status-critical

    public ConfirmationPanel()
    {
        InitializeComponent();

        SeveridadeProperty.Changed.AddClassHandler<ConfirmationPanel>((s, _) => s.AtualizarSeveridade());

        MensagemProperty.Changed.AddClassHandler<ConfirmationPanel>((s, e) =>
        {
            if (s.MensagemText != null) s.MensagemText.Text = e.NewValue as string ?? string.Empty;
        });

        PodeConfirmarProperty.Changed.AddClassHandler<ConfirmationPanel>((s, e) =>
        {
            var v = e.NewValue is true;
            if (s.ConfirmarCheckBox != null && s.ConfirmarCheckBox.IsChecked != v)
                s.ConfirmarCheckBox.IsChecked = v;
            if (s.ConfirmarButton != null)
                s.ConfirmarButton.IsEnabled = v;
        });

        ConfirmarCommandProperty.Changed.AddClassHandler<ConfirmationPanel>((s, e) =>
        {
            if (s.ConfirmarButton != null) s.ConfirmarButton.Command = e.NewValue as ICommand;
        });

        if (ConfirmarCheckBox != null)
        {
            ConfirmarCheckBox.IsCheckedChanged += (_, _) =>
                PodeConfirmar = ConfirmarCheckBox.IsChecked == true;
        }

        AtualizarSeveridade();
    }

    /// <summary>Severidade do painel — só <c>Driver</c> é usada nesta história.</summary>
    public SeveridadeConfirmacao Severidade
    {
        get => GetValue(SeveridadeProperty);
        set => SetValue(SeveridadeProperty, value);
    }

    /// <summary>Mensagem explicando a operação que será confirmada.</summary>
    public string Mensagem
    {
        get => GetValue(MensagemProperty);
        set => SetValue(MensagemProperty, value);
    }

    /// <summary>
    /// Gate do botão de aplicar. Two-way: reflete o checkbox interno de
    /// confirmação de risco e pode ser resetado externamente pelo consumidor
    /// (ex.: ao abrir o painel para um novo item).
    /// </summary>
    public bool PodeConfirmar
    {
        get => GetValue(PodeConfirmarProperty);
        set => SetValue(PodeConfirmarProperty, value);
    }

    /// <summary>Comando executado ao clicar em "Confirmar e aplicar".</summary>
    public ICommand? ConfirmarCommand
    {
        get => GetValue(ConfirmarCommandProperty);
        set => SetValue(ConfirmarCommandProperty, value);
    }

    private void AtualizarSeveridade()
    {
        var cor = Severidade == SeveridadeConfirmacao.Bios ? CorRisco : CorAviso;
        if (RootBorder != null) RootBorder.BorderBrush = cor;
    }
}
