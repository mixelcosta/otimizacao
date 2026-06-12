using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HardwareOptimizer.App.Controls;

/// <summary>
/// Custom control that renders an isometric ATX mid-tower PC case with detected
/// hardware components appearing inside the tempered glass panel.
/// Uses a basic painter's-algorithm render: grid → east face → components → glass → top.
/// </summary>
public class DiagramaGabinete : Control
{
    // ── Bindable properties ────────────────────────────────────────────────

    public static readonly StyledProperty<bool> TemCpuProperty =
        AvaloniaProperty.Register<DiagramaGabinete, bool>(nameof(TemCpu));

    public static readonly StyledProperty<bool> TemGpuProperty =
        AvaloniaProperty.Register<DiagramaGabinete, bool>(nameof(TemGpu));

    public static readonly StyledProperty<bool> TemRamProperty =
        AvaloniaProperty.Register<DiagramaGabinete, bool>(nameof(TemRam));

    public static readonly StyledProperty<bool> TemPlacaProperty =
        AvaloniaProperty.Register<DiagramaGabinete, bool>(nameof(TemPlaca));

    public static readonly StyledProperty<string> ComponenteLimitanteProperty =
        AvaloniaProperty.Register<DiagramaGabinete, string>(nameof(ComponenteLimitante), string.Empty);

    public bool TemCpu
    {
        get => GetValue(TemCpuProperty);
        set => SetValue(TemCpuProperty, value);
    }

    public bool TemGpu
    {
        get => GetValue(TemGpuProperty);
        set => SetValue(TemGpuProperty, value);
    }

    public bool TemRam
    {
        get => GetValue(TemRamProperty);
        set => SetValue(TemRamProperty, value);
    }

    public bool TemPlaca
    {
        get => GetValue(TemPlacaProperty);
        set => SetValue(TemPlacaProperty, value);
    }

    public string ComponenteLimitante
    {
        get => GetValue(ComponenteLimitanteProperty);
        set => SetValue(ComponenteLimitanteProperty, value);
    }

    static DiagramaGabinete()
    {
        AffectsRender<DiagramaGabinete>(
            TemCpuProperty, TemGpuProperty, TemRamProperty,
            TemPlacaProperty, ComponenteLimitanteProperty);
    }

    // ── Case constants (grid units) ────────────────────────────────────────
    // ATX mid-tower proportions
    private const double CW = 2.5;   // width  (east-west, x axis)
    private const double CD = 1.5;   // depth  (north-south, y axis)
    private const double CH = 5.0;   // height (z axis)

    // ── Isometric state (set per Render call) ──────────────────────────────
    private double _tw; // tile width  (screen px per 1 grid unit in x or y)
    private double _th; // tile height (= _tw / 2, 2:1 ratio)
    private Point _orig;

    // Standard isometric projection: viewer is to the South-East looking NW-up.
    //   screen_x = orig.X + (x - y) * tw/2
    //   screen_y = orig.Y + (x + y) * th/2 - z * th
    private Point Iso(double x, double y, double z) =>
        new(_orig.X + (x - y) * _tw * 0.5,
            _orig.Y + (x + y) * _th * 0.5 - z * _th);

    // ── Render ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        var b = Bounds;
        if (b.Width < 10 || b.Height < 10) return;

        // Scale so case fills ~65% of the smaller dimension
        _tw = Math.Min(b.Width, b.Height) / 5.2;
        _th = _tw * 0.5;

        // Origin: front-left-bottom corner of case sits near center-bottom
        // Shift left so the case (which extends right in x) is visually centered
        _orig = new Point(b.Width * 0.46, b.Height * 0.82);

        // 1. Background
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#080808")), new Rect(b.Size));

        // 2. Floor grid
        DrawGrid(ctx);

        // 3. Case east face (right side panel) — drawn before glass
        DrawEastFace(ctx);

        // 4. Internal components — drawn before the glass so they show through it
        DrawComponents(ctx);

        // 5. Glass panel (south face, semi-transparent) — overlays components
        DrawGlassPanel(ctx);

        // 6. Top cap
        DrawTopFace(ctx);

        // 7. Edge highlights
        DrawEdges(ctx);
    }

    // ── Grid ───────────────────────────────────────────────────────────────

    private void DrawGrid(DrawingContext ctx)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#141F14")), 0.6);
        const int R = 9;
        for (int i = -R; i <= R; i++)
        {
            ctx.DrawLine(pen, Iso(i, -R, 0), Iso(i, R, 0));  // constant-x lines
            ctx.DrawLine(pen, Iso(-R, i, 0), Iso(R, i, 0));  // constant-y lines
        }
    }

    // ── Case shell ─────────────────────────────────────────────────────────

    private static readonly Pen BorderPen =
        new(new SolidColorBrush(Color.Parse("#303030")), 1.0);

    private void DrawEastFace(DrawingContext ctx)
    {
        double hw = CW / 2, hd = CD / 2;
        Face(ctx, [
            Iso(hw, -hd, CH), Iso(hw, hd, CH),
            Iso(hw,  hd,  0), Iso(hw, -hd,  0),
        ], new SolidColorBrush(Color.Parse("#1A1A1A")), BorderPen);
    }

    private void DrawGlassPanel(DrawingContext ctx)
    {
        double hw = CW / 2, hd = CD / 2;

        // Subtle green-tinted glass — transparent enough to see components
        var glassBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint   = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(52, 5, 30, 12), 0.0),
                new GradientStop(Color.FromArgb(38, 5, 20,  8), 1.0),
            ],
        };
        var glassPen = new Pen(new SolidColorBrush(Color.Parse("#283828")), 1.0);

        Face(ctx, [
            Iso(-hw, -hd, CH), Iso(hw, -hd, CH),
            Iso(hw,  -hd,  0), Iso(-hw, -hd,  0),
        ], glassBrush, glassPen);
    }

    private void DrawTopFace(DrawingContext ctx)
    {
        double hw = CW / 2, hd = CD / 2;
        Face(ctx, [
            Iso(-hw, -hd, CH), Iso(hw, -hd, CH),
            Iso(hw,   hd, CH), Iso(-hw,  hd, CH),
        ], new SolidColorBrush(Color.Parse("#212121")), BorderPen);
    }

    private void DrawEdges(DrawingContext ctx)
    {
        double hw = CW / 2, hd = CD / 2;
        var edgePen = new Pen(new SolidColorBrush(Color.Parse("#404040")), 1.5);

        // Front-left vertical edge
        ctx.DrawLine(edgePen, Iso(-hw, -hd, 0), Iso(-hw, -hd, CH));
        // Front-right vertical edge
        ctx.DrawLine(edgePen, Iso(hw, -hd, 0), Iso(hw, -hd, CH));
        // Top front horizontal edge
        ctx.DrawLine(edgePen, Iso(-hw, -hd, CH), Iso(hw, -hd, CH));
        // Base front edge
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#333333")), 1.0),
            Iso(-hw, -hd, 0), Iso(hw, -hd, 0));
    }

    // ── Components (drawn on the glass panel face, z-depth = -CD/2 + 0.1) ─

    private void DrawComponents(DrawingContext ctx)
    {
        // y-position just inside the glass panel
        double gy = -CD / 2 + 0.12;

        DrawPsu(ctx, gy);

        if (TemPlaca)   DrawMotherboard(ctx, gy);
        if (TemCpu)     DrawCpu(ctx, gy);
        if (TemRam)     DrawRam(ctx, gy);
        if (TemGpu)     DrawGpu(ctx, gy);
    }

    private void DrawPsu(DrawingContext ctx, double gy)
    {
        // Always show PSU base block
        var fill = new SolidColorBrush(Color.Parse("#1E1E1E"));
        var pen  = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 0.7);
        Face(ctx, [
            Iso(-0.92, gy, 0.05), Iso(0.62, gy, 0.05),
            Iso( 0.62, gy, 0.92), Iso(-0.92, gy, 0.92),
        ], fill, pen);
        // PSU fan grill circle (approximated as darker rect)
        var grill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
        Face(ctx, [
            Iso(-0.72, gy, 0.15), Iso(-0.18, gy, 0.15),
            Iso(-0.18, gy, 0.80), Iso(-0.72, gy, 0.80),
        ], grill, null);
    }

    private void DrawMotherboard(DrawingContext ctx, double gy)
    {
        var fill = new SolidColorBrush(Color.FromArgb(200, 12, 48, 22));
        var pen  = new Pen(new SolidColorBrush(Color.FromArgb(170, 0, 160, 60)), 0.7);
        Face(ctx, [
            Iso(-0.90, gy, 1.05), Iso(0.72, gy, 1.05),
            Iso( 0.72, gy, 4.30), Iso(-0.90, gy, 4.30),
        ], fill, pen);
    }

    private void DrawCpu(DrawingContext ctx, double gy)
    {
        bool bot  = ComponenteLimitante.Equals("CPU", StringComparison.OrdinalIgnoreCase);
        var col   = bot ? Color.Parse("#FF4500") : Color.Parse("#0090FF");
        var fill  = new SolidColorBrush(Color.FromArgb(240, col.R, col.G, col.B));
        var pen   = new Pen(new SolidColorBrush(col), 0.9);

        // Cooler (larger square, drawn first so chip sits on top)
        var cooler = new SolidColorBrush(Color.FromArgb(150, 35, 110, 160));
        Face(ctx, [
            Iso(-0.78, gy, 2.72), Iso(0.06, gy, 2.72),
            Iso( 0.06, gy, 3.78), Iso(-0.78, gy, 3.78),
        ], cooler, null);

        // CPU IHS (integrated heat spreader)
        Face(ctx, [
            Iso(-0.64, gy, 2.92), Iso(-0.12, gy, 2.92),
            Iso(-0.12, gy, 3.56), Iso(-0.64, gy, 3.56),
        ], fill, pen);
    }

    private void DrawRam(DrawingContext ctx, double gy)
    {
        var stick = new SolidColorBrush(Color.FromArgb(220, 0, 205, 95));
        var glow  = new SolidColorBrush(Color.FromArgb(130, 0, 255, 120));

        // Two DDR sticks side by side
        Point[][] sticks =
        [
            [Iso(0.08, gy, 2.82), Iso(0.30, gy, 2.82), Iso(0.30, gy, 4.15), Iso(0.08, gy, 4.15)],
            [Iso(0.34, gy, 2.82), Iso(0.56, gy, 2.82), Iso(0.56, gy, 4.15), Iso(0.34, gy, 4.15)],
        ];

        foreach (var pts in sticks)
        {
            Face(ctx, pts, stick, null);
            // RGB stripe just above the top of each stick (screen Y decreases upward)
            var glow0 = new Point(pts[3].X, pts[3].Y - 3);
            var glow1 = new Point(pts[2].X, pts[2].Y - 3);
            Face(ctx, [glow0, glow1, pts[2], pts[3]], glow, null);
        }
    }

    private void DrawGpu(DrawingContext ctx, double gy)
    {
        bool bot  = ComponenteLimitante.Equals("GPU", StringComparison.OrdinalIgnoreCase);
        var col   = bot ? Color.Parse("#FF4500") : Color.Parse("#8833EE");
        var fill  = new SolidColorBrush(Color.FromArgb(230, col.R, col.G, col.B));
        var pen   = new Pen(new SolidColorBrush(col), 1.0);
        var dark  = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));

        // Main PCB bar
        Face(ctx, [
            Iso(-0.88, gy, 1.78), Iso(0.88, gy, 1.78),
            Iso( 0.88, gy, 2.38), Iso(-0.88, gy, 2.38),
        ], fill, pen);

        // Three fan cut-outs
        double[][] fans = [[-0.82, -0.32], [-0.28, 0.22], [0.26, 0.76]];
        foreach (var fan in fans)
        {
            Face(ctx, [
                Iso(fan[0], gy, 1.82), Iso(fan[1], gy, 1.82),
                Iso(fan[1], gy, 2.34), Iso(fan[0], gy, 2.34),
            ], dark, null);
        }
    }

    // ── Helper ─────────────────────────────────────────────────────────────

    private static void Face(DrawingContext ctx, Point[] pts, IBrush? fill, IPen? pen)
    {
        var geo = new StreamGeometry();
        {
            using var gctx = geo.Open();
            gctx.BeginFigure(pts[0], isFilled: true);
            for (int i = 1; i < pts.Length; i++) gctx.LineTo(pts[i]);
            gctx.EndFigure(true);
        }
        ctx.DrawGeometry(fill, pen, geo);
    }
}
