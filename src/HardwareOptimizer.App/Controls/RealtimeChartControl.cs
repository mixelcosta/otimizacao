using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HardwareOptimizer.App.Controls;

/// <summary>
/// Controle Canvas de linha em tempo real. Mantém uma janela deslizante de 60 amostras
/// e redesenha sem dependência de bibliotecas externas de charting.
/// </summary>
public sealed class RealtimeChartControl : Control
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RealtimeChartControl, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Color> LinhaCorProperty =
        AvaloniaProperty.Register<RealtimeChartControl, Color>(nameof(LinhaCor), Colors.Cyan);

    public static readonly StyledProperty<double> MaximoProperty =
        AvaloniaProperty.Register<RealtimeChartControl, double>(nameof(Maximo), 100.0);

    public static readonly StyledProperty<double> MinimoProperty =
        AvaloniaProperty.Register<RealtimeChartControl, double>(nameof(Minimo), 0.0);

    private const int CapacidadeMaxima = 60;
    private readonly Queue<double> _valores = new(CapacidadeMaxima + 1);

    static RealtimeChartControl()
    {
        AffectsRender<RealtimeChartControl>(LabelProperty, LinhaCorProperty, MaximoProperty, MinimoProperty);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Color LinhaCor
    {
        get => GetValue(LinhaCorProperty);
        set => SetValue(LinhaCorProperty, value);
    }

    public double Maximo
    {
        get => GetValue(MaximoProperty);
        set => SetValue(MaximoProperty, value);
    }

    public double Minimo
    {
        get => GetValue(MinimoProperty);
        set => SetValue(MinimoProperty, value);
    }

    public void AdicionarValor(double valor)
    {
        if (_valores.Count >= CapacidadeMaxima)
            _valores.Dequeue();
        _valores.Enqueue(valor);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var fundo = new SolidColorBrush(Color.Parse("#0A0A0A"));
        var grade = new SolidColorBrush(Color.Parse("#1A1A1A"));
        var linhaPen = new Pen(new SolidColorBrush(LinhaCor), 1.5);
        var gradePen = new Pen(grade, 1);
        var textBrush = new SolidColorBrush(Color.Parse("#888888"));

        context.DrawRectangle(fundo, null, new Rect(0, 0, w, h));

        // Linhas de grade horizontais a cada 25%
        for (int i = 1; i < 4; i++)
        {
            double y = h * i / 4.0;
            context.DrawLine(gradePen, new Point(0, y), new Point(w, y));
        }

        var pontos = _valores.ToArray();
        if (pontos.Length < 2) return;

        double amplitude = Maximo - Minimo;
        if (amplitude <= 0) amplitude = 1;
        double passoX = w / (CapacidadeMaxima - 1.0);

        var geometria = new StreamGeometry();
        using (var ctx = geometria.Open())
        {
            for (int i = 0; i < pontos.Length; i++)
            {
                double xOffset = (CapacidadeMaxima - pontos.Length + i) * passoX;
                double valorNorm = Math.Clamp((pontos[i] - Minimo) / amplitude, 0, 1);
                double y = h - valorNorm * h;

                if (i == 0)
                    ctx.BeginFigure(new Point(xOffset, y), false);
                else
                    ctx.LineTo(new Point(xOffset, y));
            }
        }

        context.DrawGeometry(null, linhaPen, geometria);

        // Valor atual no canto superior direito
        if (pontos.Length > 0)
        {
            var ft = new FormattedText(
                $"{Label}: {pontos[^1]:F0}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                textBrush);
            context.DrawText(ft, new Point(4, 4));
        }
    }
}
