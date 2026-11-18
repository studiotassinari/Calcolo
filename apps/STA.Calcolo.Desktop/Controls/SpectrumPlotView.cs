using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using STA.Calcolo.Application;
namespace STA.Calcolo.Desktop.Controls;

/// <summary>
/// Grafico degli spettri: asse delle ordinate principale in Se/g, asse secondario a destra in m/s² (g = 9,81 m/s²).
/// Al passaggio del mouse una linea verticale segue il cursore e un riquadro riporta il periodo T e, per ogni curva,
/// il valore interpolato in g e in m/s². Il controllo disegna soltanto curve già calcolate dal ViewModel.
/// </summary>
public sealed class SpectrumPlotView : FrameworkElement
{
    public static readonly DependencyProperty CurvesProperty = DependencyProperty.Register(nameof(Curves), typeof(IReadOnlyList<SpectrumCurve>), typeof(SpectrumPlotView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(nameof(Highlight), typeof(string), typeof(SpectrumPlotView), new FrameworkPropertyMetadata("SLV", FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<SpectrumCurve>? Curves { get => (IReadOnlyList<SpectrumCurve>?)GetValue(CurvesProperty); set => SetValue(CurvesProperty, value); }
    /// <summary>Stato limite evidenziato (tratto più spesso).</summary>
    public string Highlight { get => (string)GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }

    /// <summary>Periodo sotto il cursore [s], null se il mouse è fuori dall'area del grafico. Esposto per i test.</summary>
    public double? CursorPeriod { get; private set; }
    private const double Left = 64, Right = 70, Top = 72, Bottom = 46;
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static Brush Brush(string c) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)); b.Freeze(); return b; }
    private static readonly Dictionary<string, Brush> Colors = new()
    { ["SLO"] = Brush("#2E7D4F"), ["SLD"] = Brush("#226BA0"), ["SLV"] = Brush("#A35B18"), ["SLC"] = Brush("#B3261E") };
    private static readonly Brush Navy = Brush("#192845"), Muted = Brush("#526778"), Grid = Brush("#E3EAEC");

    private (double MaxT, double MaxY) Range()
    {
        var points = Curves?.SelectMany(c => c.Points).ToArray() ?? [];
        if (points.Length == 0) return (4, 1);
        var maxY = points.Max(p => p.Y);
        return (Math.Max(.1, points.Max(p => p.X)), Nice(maxY <= 0 ? 1 : maxY * 1.08));
    }
    /// <summary>Arrotonda il fondo scala a 1, 2 o 5 × 10ⁿ per tacche leggibili.</summary>
    private static double Nice(double v)
    {
        var e = Math.Pow(10, Math.Floor(Math.Log10(v))); var m = v / e;
        return (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * e;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this); var width = ActualWidth - Left - Right;
        SetCursor(p.X >= Left && p.X <= Left + width && p.Y >= Top && p.Y <= ActualHeight - Bottom ? (p.X - Left) / width * Range().MaxT : null);
    }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); SetCursor(null); }
    /// <summary>Posiziona il cursore a un periodo dato (usato anche dai test).</summary>
    public void SetCursor(double? period) { if (CursorPeriod == period) return; CursorPeriod = period; InvalidateVisual(); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        double width = ActualWidth - Left - Right, height = ActualHeight - Top - Bottom;
        if (width < 120 || height < 80) return;
        void Text(string s, double x, double y, Brush b, double size = 11, bool right = false)
        {
            var ft = new FormattedText(s, It, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new(right ? x - ft.Width : x, y));
        }
        var (maxT, maxY) = Range();
        Point Map(double t, double y) => new(Left + t / maxT * width, Top + height - y / maxY * height);
        // Griglia, assi e tacche: Se/g a sinistra, m/s² a destra, periodo in basso.
        var gridPen = new Pen(Grid, 1); var axis = new Pen(Muted, 1);
        for (var i = 0; i <= 5; i++)
        {
            var y = maxY * i / 5; var py = Map(0, y).Y;
            dc.DrawLine(gridPen, new(Left, py), new(Left + width, py));
            Text(y.ToString("0.###", It), Left - 6, py - 8, Muted, 11, true);
            Text((y * SeismicSpectra.G).ToString("0.##", It), Left + width + 6, py - 8, Muted);
        }
        var stepT = maxT <= 2 ? .25 : maxT <= 5 ? .5 : 1;
        for (var t = 0d; t <= maxT + 1e-9; t += stepT)
        {
            var px = Map(t, 0).X; dc.DrawLine(gridPen, new(px, Top), new(px, Top + height));
            Text(t.ToString("0.##", It), px - 8, Top + height + 4, Muted);
        }
        dc.DrawRectangle(null, axis, new Rect(Left, Top, width, height));
        Text("Se/g", 8, Top - 20, Navy, 12); Text("[m/s²]", Left + width + 6, Top - 20, Navy, 12);
        Text("Periodo T [s]", Left + width / 2 - 36, Top + height + 22, Navy, 12);
        if (Curves is not { Count: > 0 } curves) { Text("Correggere i dati per visualizzare gli spettri.", Left + 12, Top + 12, Muted, 12); return; }
        // Curve: colore per stato limite, tratteggio per il verticale, tratto pieno spesso per il progetto e per lo stato evidenziato.
        double legendX = Left, legendY = 6; // legenda su più righe sopra il grafico
        foreach (var c in curves)
        {
            var brush = Colors.TryGetValue(c.LimitState, out var b) ? b : Navy;
            var pen = new Pen(brush, c.LimitState == Highlight || c.Design ? 2.6 : 1.4);
            if (c.Vertical) pen.DashStyle = DashStyles.Dash;
            if (c.Design) pen.DashStyle = new DashStyle([6, 2, 1, 2], 0);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open()) { g.BeginFigure(Map(c.Points[0].X, c.Points[0].Y), false, false); g.PolyLineTo(c.Points.Skip(1).Select(p => Map(p.X, p.Y)).ToArray(), true, false); }
            geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
            var itemWidth = 32 + c.Name.Length * 6.5;
            if (legendX + itemWidth > Left + width) { legendX = Left; legendY += 16; }
            dc.DrawLine(pen, new(legendX, legendY + 8), new(legendX + 18, legendY + 8)); Text(c.Name, legendX + 22, legendY, brush); legendX += itemWidth;
        }
        // Cursore: linea verticale, punti sulle curve e riquadro con T e valori in g e m/s².
        if (CursorPeriod is not double period) return;
        var x = Map(period, 0).X;
        dc.DrawLine(new Pen(Navy, 1) { DashStyle = DashStyles.Dot }, new(x, Top), new(x, Top + height));
        var lines = new List<(string Text, Brush Brush)> { ($"T = {period.ToString("0.000", It)} s", Navy) };
        foreach (var c in curves)
        {
            var value = SeismicSpectra.At(c.Points, period); var brush = Colors.TryGetValue(c.LimitState, out var b) ? b : Navy;
            dc.DrawEllipse(Brushes.White, new Pen(brush, 2), Map(period, value), 3.5, 3.5);
            lines.Add(($"{c.Name}: {value.ToString("0.0000", It)} g · {(value * SeismicSpectra.G).ToString("0.000", It)} m/s²", brush));
        }
        double boxWidth = 250, boxHeight = 8 + 16 * lines.Count;
        var boxX = x + 12 + boxWidth > Left + width ? x - 12 - boxWidth : x + 12; var boxY = Top + 8;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), new Pen(Grid, 1), new Rect(boxX, boxY, boxWidth, boxHeight), 3, 3);
        for (var i = 0; i < lines.Count; i++) Text(lines[i].Text, boxX + 8, boxY + 4 + 16 * i, lines[i].Brush, i == 0 ? 12 : 11);
    }
}
