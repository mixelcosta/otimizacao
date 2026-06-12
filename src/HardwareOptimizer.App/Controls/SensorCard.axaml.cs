using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HardwareOptimizer.App.Controls;

public partial class SensorCard : UserControl
{
    public static readonly StyledProperty<string> TituloProperty =
        AvaloniaProperty.Register<SensorCard, string>(nameof(Titulo), string.Empty);

    public static readonly StyledProperty<string> ValorProperty =
        AvaloniaProperty.Register<SensorCard, string>(nameof(Valor), "--");

    public static readonly StyledProperty<string> SubtituloProperty =
        AvaloniaProperty.Register<SensorCard, string>(nameof(Subtitulo), string.Empty);

    /// <summary>Nível 0=estável(verde) 1=alerta(amarelo) 2=crítico(vermelho).</summary>
    public static readonly StyledProperty<int> NivelAlertaProperty =
        AvaloniaProperty.Register<SensorCard, int>(nameof(NivelAlerta), 0);

    private static readonly IBrush CorEstavel = new SolidColorBrush(Color.Parse("#00FF88"));
    private static readonly IBrush CorAlerta = new SolidColorBrush(Color.Parse("#FFCC00"));
    private static readonly IBrush CorCritico = new SolidColorBrush(Color.Parse("#FF3333"));

    public SensorCard()
    {
        InitializeComponent();
        NivelAlertaProperty.Changed.AddClassHandler<SensorCard>((s, _) => s.AtualizarCor());
        ValorProperty.Changed.AddClassHandler<SensorCard>((s, e) =>
        {
            if (s.ValorText != null)
                s.ValorText.Text = e.NewValue as string ?? "--";
        });
        TituloProperty.Changed.AddClassHandler<SensorCard>((s, e) =>
        {
            if (s.TituloText != null)
                s.TituloText.Text = e.NewValue as string ?? string.Empty;
        });
        SubtituloProperty.Changed.AddClassHandler<SensorCard>((s, e) =>
        {
            if (s.SubtituloText != null)
                s.SubtituloText.Text = e.NewValue as string ?? string.Empty;
        });
    }

    public string Titulo
    {
        get => GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string Valor
    {
        get => GetValue(ValorProperty);
        set => SetValue(ValorProperty, value);
    }

    public string Subtitulo
    {
        get => GetValue(SubtituloProperty);
        set => SetValue(SubtituloProperty, value);
    }

    public int NivelAlerta
    {
        get => GetValue(NivelAlertaProperty);
        set => SetValue(NivelAlertaProperty, value);
    }

    private void AtualizarCor()
    {
        if (ValorText is null) return;
        ValorText.Foreground = NivelAlerta switch
        {
            1 => CorAlerta,
            2 => CorCritico,
            _ => CorEstavel,
        };
    }
}
