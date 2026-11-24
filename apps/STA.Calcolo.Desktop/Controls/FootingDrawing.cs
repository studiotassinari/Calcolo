using System.Globalization;
using System.Windows;
using System.Windows.Media;
using STA.Plinti.Core;
namespace STA.Calcolo.Desktop.Controls;

/// <summary>
/// Disegno del plinto per la combinazione selezionata (solo presentazione, nessun calcolo proprio):
/// a sinistra la pianta con pilastro, nocciolo centrale, risultante, base efficace B′×L′, zona non reagente e perimetro a 2d;
/// a destra la sezione lungo X con armatura e diagramma delle pressioni sul terreno σ(x) sull'asse della risultante.
/// </summary>
public sealed class FootingDrawing : FrameworkElement
{
    public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register(nameof(Parameters), typeof(FootingParameters), typeof(FootingDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(nameof(Result), typeof(FootingDesignResult), typeof(FootingDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public FootingParameters? Parameters { get => (FootingParameters?)GetValue(ParametersProperty); set => SetValue(ParametersProperty, value); }
    public FootingDesignResult? Result { get => (FootingDesignResult?)GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static Brush B(string c) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)); b.Freeze(); return b; }
    private static readonly Brush Navy = B("#192845"), Teal = B("#135F6E"), Concrete = B("#C9DFE2"), Column = B("#8FB3BA"), Amber = B("#A35B18"),
        Pressure = B("#F4DBC0"), Muted = B("#526778"), Red = B("#B3261E"), Purple = B("#6B4C9A"), Soil = B("#E6DFCE"), Grey = B("#D9DEE1");
    private static string N(double v, string f = "0.##") => v.ToString(f, It);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        void Text(string s, double x, double y, Brush b, double size = 11, bool center = false)
        {
            var ft = new FormattedText(s, It, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new(center ? x - ft.Width / 2 : x, y));
        }
        if (Parameters is not { } p || ActualWidth < 300 || ActualHeight < 180) { Text("Correggere i dati per visualizzare il plinto.", 12, 12, Muted, 12); return; }
        var r = Result;
        double half = ActualWidth / 2, margin = 34, top = 30;
        Text("Pianta", 12, 6, Navy, 12); Text("Sezione lungo X" + (r is null ? "" : " · pressioni sul terreno"), half + 12, 6, Navy, 12);

        // ── Pianta: scala uguale sui due assi, origine al centro della base.
        double scale = Math.Min((half - 2 * margin) / p.B, (ActualHeight - top - margin - 18) / p.L);
        double cx0 = half / 2, cy0 = top + (ActualHeight - top - 18) / 2;
        Point P(double x, double y) => new(cx0 + x * scale, cy0 - y * scale);
        Rect R(double x0, double y0, double x1, double y1) => new(P(x0, y1), P(x1, y0));
        dc.DrawRectangle(Concrete, new Pen(Teal, 1.5), R(-p.B / 2, -p.L / 2, p.B / 2, p.L / 2));
        if (r is { Pressure.Valid: true } && r.Pressure.ContactRatio < 1)
        {
            // Zona non reagente: celle con σ = 0, campionate per disegnare la porzione di base staccata dal terreno.
            const int n = 60; double dx = p.B / n, dy = p.L / n;
            for (var i = 0; i < n; i++) for (var j = 0; j < n; j++)
            {
                double x = -p.B / 2 + (i + .5) * dx, y = -p.L / 2 + (j + .5) * dy;
                if (r.Pressure.Plane.At(x, y) <= 0) dc.DrawRectangle(Grey, null, R(x - dx / 2, y - dy / 2, x + dx / 2, y + dy / 2));
            }
        }
        // Nocciolo centrale (rombo B/6, L/6) e perimetro di controllo a 2d dal pilastro.
        var kern = new StreamGeometry();
        using (var g = kern.Open()) { g.BeginFigure(P(p.B / 6, 0), false, true); g.PolyLineTo([P(0, p.L / 6), P(-p.B / 6, 0), P(0, -p.L / 6)], true, false); }
        kern.Freeze(); dc.DrawGeometry(null, new Pen(Muted, 1) { DashStyle = DashStyles.Dash }, kern);
        var d = r?.D ?? p.H - p.Cover / 1000 - 1.5 * p.Diameter / 1000;
        if (d > 0) dc.DrawRoundedRectangle(null, new Pen(Purple, 1.2) { DashStyle = DashStyles.Dot }, R(-p.Cx / 2 - 2 * d, -p.Cy / 2 - 2 * d, p.Cx / 2 + 2 * d, p.Cy / 2 + 2 * d), 2 * d * scale, 2 * d * scale);
        dc.DrawRectangle(Column, new Pen(Navy, 1.2), R(-p.Cx / 2, -p.Cy / 2, p.Cx / 2, p.Cy / 2));
        Text($"B = {N(p.B)} m", P(0, -p.L / 2).X, P(0, -p.L / 2).Y + 4, Navy, 11, true);
        Text($"L = {N(p.L)} m", P(p.B / 2, 0).X + 4, P(p.B / 2, 0).Y - 8, Navy);
        Text("perimetro 2d", P(p.Cx / 2 + 2 * d, p.Cy / 2 + 2 * d).X - 30, P(p.Cx / 2 + 2 * d, p.Cy / 2 + 2 * d).Y - 16, Purple, 10);
        if (r is not null && double.IsFinite(r.Ex) && double.IsFinite(r.Ey))
        {
            // Base efficace di Meyerhof centrata sulla risultante e punto di applicazione di N.
            if (r.BEff > 0 && r.LEff > 0)
                dc.DrawRectangle(null, new Pen(Amber, 1.4) { DashStyle = DashStyles.Dash }, R(r.Ex - r.BEff / 2, r.Ey - r.LEff / 2, r.Ex + r.BEff / 2, r.Ey + r.LEff / 2));
            var e = P(r.Ex, r.Ey); var inKern = Math.Abs(r.Ex) / (p.B / 6) + Math.Abs(r.Ey) / (p.L / 6) <= 1;
            dc.DrawEllipse(inKern ? Teal : Red, new Pen(Brushes.White, 1.5), e, 5, 5);
            Text($"N = {N(r.N, "0")} kN · e = ({N(r.Ex, "0.###")}; {N(r.Ey, "0.###")}) m", 10, ActualHeight - 18, inKern ? Teal : Red, 11);
            if (r.Pressure.Valid)
            {
                // Pressioni ai vertici [kPa].
                foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                {
                    var v = r.Pressure.Plane.At(sx * p.B / 2, sy * p.L / 2); var c = P(sx * p.B / 2, sy * p.L / 2);
                    Text(N(v, "0"), c.X + (sx > 0 ? 3 : -30), c.Y + (sy > 0 ? -16 : 2), Amber, 10);
                }
            }
        }

        // ── Sezione lungo X: plinto, pilastro, armatura inferiore, terreno e diagramma σ(x) sull'asse y = ey.
        double sx0 = half + margin, sWidth = half - 2 * margin;
        double sScale = Math.Min(sWidth / p.B, (ActualHeight - top - 120) / (p.H + .8));
        double baseY = top + 30 + (p.H + .6) * sScale;
        Point S(double x, double z) => new(sx0 + sWidth / 2 + x * sScale, baseY - z * sScale);
        dc.DrawRectangle(Soil, null, new Rect(S(-p.B / 2 - .2, 0), S(p.B / 2 + .2, -.15)));
        dc.DrawRectangle(Concrete, new Pen(Teal, 1.5), new Rect(S(-p.B / 2, p.H), S(p.B / 2, 0)));
        dc.DrawRectangle(Column, new Pen(Navy, 1.2), new Rect(S(-p.Cx / 2, p.H + .6), S(p.Cx / 2, p.H)));
        var cover = p.Cover / 1000 + p.Diameter / 2000;
        dc.DrawLine(new Pen(Red, 2), S(-p.B / 2 + p.Cover / 1000, cover), S(p.B / 2 - p.Cover / 1000, cover));
        Text($"h = {N(p.H)} m · d = {N(d, "0.000")} m · Ø{N(p.Diameter, "0")}/{N(p.Spacing, "0")}", sx0, S(0, p.H + .6).Y - 18, Navy);
        if (r is { Pressure.Valid: true })
        {
            const int n = 80; var values = Enumerable.Range(0, n + 1).Select(i => -p.B / 2 + p.B * i / n).Select(x => (x, s: r.Pressure.Plane.At(x, double.IsFinite(r.Ey) ? Math.Clamp(r.Ey, -p.L / 2, p.L / 2) : 0))).ToArray();
            var max = Math.Max(1e-9, values.Max(v => v.s)); var k = 70 / max;
            var shape = new StreamGeometry();
            using (var g = shape.Open())
            {
                g.BeginFigure(S(-p.B / 2, -.15), true, true);
                foreach (var (x, s) in values) g.LineTo(new(S(x, -.15).X, S(x, -.15).Y + s * k), true, false);
                g.LineTo(S(p.B / 2, -.15), true, false);
            }
            shape.Freeze(); dc.DrawGeometry(Pressure, new Pen(Amber, 1.3), shape);
            Text($"σ: {N(values.First().s, "0")} … {N(values.Last().s, "0")} kPa (max {N(r.Pressure.SigmaMax, "0")} kPa, area reagente {N(r.Pressure.ContactRatio * 100, "0")} %)", sx0, S(0, -.15).Y + 76, Amber, 11);
            // Freccia del carico verticale al pilastro.
            var head = S(0, p.H + .6); dc.DrawLine(new Pen(Navy, 2), new(head.X, head.Y - 34), head);
            dc.DrawLine(new Pen(Navy, 2), head, new(head.X - 5, head.Y - 9)); dc.DrawLine(new Pen(Navy, 2), head, new(head.X + 5, head.Y - 9));
        }
        else if (r is not null) Text(r.Pressure.Message, sx0, S(0, -.15).Y + 10, Red, 11);
        Text("— armatura inferiore", sx0, ActualHeight - 18, Red, 10); Text("◇ nocciolo · ▭ B′×L′ · ░ zona non reagente", sx0 + 120, ActualHeight - 18, Muted, 10);
    }
}
