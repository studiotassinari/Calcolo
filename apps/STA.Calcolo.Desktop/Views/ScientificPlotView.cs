using System.Globalization;
using System.Windows;
using System.Windows.Media;
using STA.Calcolo.Application;
namespace STA.Calcolo.Desktop.Views;

/// <summary>Disegna esclusivamente serie già calcolate: assi con unità, legenda e frecce del profilo vento.</summary>
public sealed class ScientificPlotView : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(IReadOnlyList<PlotSeries>), typeof(ScientificPlotView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty XAxisProperty = DependencyProperty.Register(nameof(XAxis), typeof(string), typeof(ScientificPlotView), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty YAxisProperty = DependencyProperty.Register(nameof(YAxis), typeof(string), typeof(ScientificPlotView), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(nameof(Profile), typeof(bool), typeof(ScientificPlotView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<PlotSeries>? Series { get => (IReadOnlyList<PlotSeries>?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public string XAxis { get => (string)GetValue(XAxisProperty); set => SetValue(XAxisProperty, value); }
    public string YAxis { get => (string)GetValue(YAxisProperty); set => SetValue(YAxisProperty, value); }
    public bool Profile { get => (bool)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        if (ActualWidth < 220 || ActualHeight < 160) return;
        void Text(string text, double x, double y, Brush brush, double size = 12) =>
            dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
        var all = Series?.SelectMany(s => s.Points).ToArray();
        Text(YAxis, 12, 5, Brushes.DarkSlateGray); Text(XAxis, 70, ActualHeight - 24, Brushes.DarkSlateGray);
        if (all == null || all.Length == 0) { Text("Premere Calcola per aggiornare il grafico", 70, 90, Brushes.Gray); return; }
        double minX = Math.Min(0, all.Min(p => p.X)), maxX = Math.Max(0, all.Max(p => p.X));
        double minY = Math.Min(0, all.Min(p => p.Y)), maxY = Math.Max(0, all.Max(p => p.Y));
        if (maxX == minX) maxX = minX + 1;
        if (maxY == minY) maxY = minY + 1;
        double width = ActualWidth - 100, top = 72, height = ActualHeight - 122;
        Point Map(PlotPoint p) => new(72 + (p.X - minX) / (maxX - minX) * width, top + height - (p.Y - minY) / (maxY - minY) * height);
        for (int n = 0; n <= 4; n++)
        {
            double x = minX + (maxX - minX) * n / 4, y = minY + (maxY - minY) * n / 4;
            var a = Map(new(x, minY)); var b = Map(new(minX, y));
            dc.DrawLine(new(Brushes.Gainsboro, 1), a, Map(new(x, maxY)));
            dc.DrawLine(new(Brushes.Gainsboro, 1), b, Map(new(maxX, y)));
            Text(x.ToString("0.##"), a.X - 15, a.Y + 6, Brushes.DimGray, 11);
            Text(y.ToString("0.##"), 8, b.Y - 8, Brushes.DimGray, 11);
        }
        Brush[] colors = [new SolidColorBrush(Color.FromRgb(19,95,110)), Brushes.DarkOrange, Brushes.ForestGreen, Brushes.MediumVioletRed, Brushes.RoyalBlue];
        int index = 0; double legendX = 12, legendY = 28;
        foreach (var series in Series!)
        {
            if (legendX + 120 > ActualWidth) { legendX = 12; legendY += 18; }
            var color = colors[index++ % colors.Length];
            Text(series.Name, legendX, legendY, color); legendX += 120;
            if (series.Points.Count == 0) continue;
            if (!series.Markers)
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open()) { g.BeginFigure(Map(series.Points[0]), false, false); g.PolyLineTo(series.Points.Skip(1).Select(Map).ToArray(), true, false); }
                dc.DrawGeometry(null, new(color, series.Name.Contains("scelta") ? 4 : 2), geometry);
            }
            else foreach (var p in series.Points) dc.DrawEllipse(color, new(Brushes.White, 1), Map(p), 5, 5);
            // Frecce orientate secondo il segno di p; nessun valore è ricavato dal disegno.
            if (Profile)
                foreach (var p in series.Points.Where((_, n) => n % 10 == 0 && n > 0))
                {
                    var a = Map(new(0, p.Y)); var b = Map(p); dc.DrawLine(new(color, 1), a, b);
                    if (Math.Abs(b.X - a.X) > 8)
                    { double sign = Math.Sign(b.X - a.X); dc.DrawLine(new(color, 1), b, new(b.X - 6 * sign, b.Y - 3)); dc.DrawLine(new(color, 1), b, new(b.X - 6 * sign, b.Y + 3)); }
                }
        }
    }
}
