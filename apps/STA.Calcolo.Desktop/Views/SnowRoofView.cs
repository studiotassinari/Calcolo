using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace STA.Calcolo.Desktop.Views;

/// <summary>Schema di una falda, con angolo geometrico reale e frecce verticali equidistanti in proiezione.</summary>
public sealed class SnowRoofView : FrameworkElement
{
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(nameof(Angle), typeof(double), typeof(SnowRoofView), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LoadProperty = DependencyProperty.Register(nameof(Load), typeof(double), typeof(SnowRoofView), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }
    public double Load { get => (double)GetValue(LoadProperty); set => SetValue(LoadProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); if (ActualWidth < 180 || ActualHeight < 150) return;
        void Text(string value, double x, double y) => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.DarkSlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
        double a = Math.Clamp(Angle, 0, 90) * Math.PI / 180;
        double length = Math.Min((ActualWidth - 100) / Math.Max(.001, Math.Cos(a)), (ActualHeight - 110) / Math.Max(.001, Math.Sin(a)));
        var start = new Point(50, ActualHeight - 40); var end = new Point(start.X + length * Math.Cos(a), start.Y - length * Math.Sin(a));
        dc.DrawLine(new(Brushes.DarkSlateGray, 5), start, end);
        dc.DrawLine(new(Brushes.Gray, 1) { DashStyle = DashStyles.Dash }, start, new(end.X + 25, start.Y));
        Text($"α = {Angle:0.#}°", 60, ActualHeight - 28);
        Text(double.IsFinite(Load) ? $"qs = {Load:0.###} kN/m² · proiezione orizzontale" : "Schema geometrico · carico da calcolare", 12, 6);
        // A qs nullo non si disegnano frecce di carico; a 90° la proiezione si annulla.
        if (double.IsFinite(Load) && Load > 0 && end.X - start.X > 10)
            for (int n = 1; n <= 7; n++)
            {
                double x = start.X + (end.X - start.X) * n / 8, y = start.Y + (end.Y - start.Y) * n / 8 - 6;
                var pen = new Pen(Brushes.Teal, 2);
                dc.DrawLine(pen, new(x, y - 32), new(x, y));
                dc.DrawLine(pen, new(x, y), new(x - 4, y - 7)); dc.DrawLine(pen, new(x, y), new(x + 4, y - 7));
            }
    }
}
