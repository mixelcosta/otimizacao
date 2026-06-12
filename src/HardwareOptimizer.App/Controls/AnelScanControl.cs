using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace HardwareOptimizer.App.Controls;

/// <summary>
/// Segmented ring that fills clockwise as Progresso goes from 0→1.
/// When Escaneando=true, the leading-edge segment pulses green.
/// Inspired by Driver Booster's tachometer-style scan ring.
/// </summary>
public class AnelScanControl : Control
{
    public static readonly StyledProperty<double> ProgressoProperty =
        AvaloniaProperty.Register<AnelScanControl, double>(nameof(Progresso));

    public static readonly StyledProperty<bool> EscaneandoProperty =
        AvaloniaProperty.Register<AnelScanControl, bool>(nameof(Escaneando));

    public double Progresso
    {
        get => GetValue(ProgressoProperty);
        set => SetValue(ProgressoProperty, value);
    }

    public bool Escaneando
    {
        get => GetValue(EscaneandoProperty);
        set => SetValue(EscaneandoProperty, value);
    }

    static AnelScanControl()
    {
        AffectsRender<AnelScanControl>(ProgressoProperty, EscaneandoProperty);
    }

    private DispatcherTimer? _timer;
    private bool _pulso;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == EscaneandoProperty)
        {
            if (Escaneando)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
                _timer.Tick += (_, _) => { _pulso = !_pulso; InvalidateVisual(); };
                _timer.Start();
            }
            else
            {
                _timer?.Stop();
                _timer = null;
                _pulso = false;
                InvalidateVisual();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    public override void Render(DrawingContext ctx)
    {
        var b = Bounds;
        if (b.Width < 20) return;

        double cx = b.Width / 2;
        double cy = b.Height / 2;
        double outerR = Math.Min(cx, cy) - 4;
        double innerR = outerR - 22;

        const int N = 20;
        const double gapDeg = 6.0;
        double segDeg = (360.0 / N) - gapDeg;

        int litCount = Math.Clamp((int)Math.Round(Progresso * N), 0, N);

        for (int i = 0; i < N; i++)
        {
            double startDeg = -90.0 + i * (360.0 / N);

            Color col = (i < litCount)              ? Color.Parse("#00C870")
                      : (i == litCount && Escaneando && _pulso) ? Color.Parse("#55FF99")
                      :                               Color.Parse("#1C1C1C");

            DrawSeg(ctx, new Point(cx, cy), innerR, outerR, startDeg, startDeg + segDeg,
                new SolidColorBrush(col));
        }

        // Fill inside of ring with background colour so the button beneath is visible
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#050505")), null,
            new Point(cx, cy), innerR - 2, innerR - 2);
    }

    private static void DrawSeg(DrawingContext ctx, Point c,
        double ri, double ro, double startDeg, double endDeg, IBrush fill)
    {
        double s = startDeg * Math.PI / 180.0;
        double e = endDeg * Math.PI / 180.0;
        bool large = (endDeg - startDeg) > 180;

        var p1 = new Point(c.X + ro * Math.Cos(s), c.Y + ro * Math.Sin(s));
        var p2 = new Point(c.X + ro * Math.Cos(e), c.Y + ro * Math.Sin(e));
        var p3 = new Point(c.X + ri * Math.Cos(e), c.Y + ri * Math.Sin(e));
        var p4 = new Point(c.X + ri * Math.Cos(s), c.Y + ri * Math.Sin(s));

        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(p1, isFilled: true);
            gc.ArcTo(p2, new Size(ro, ro), 0, large, SweepDirection.Clockwise);
            gc.LineTo(p3);
            gc.ArcTo(p4, new Size(ri, ri), 0, large, SweepDirection.CounterClockwise);
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(fill, null, geo);
    }
}
