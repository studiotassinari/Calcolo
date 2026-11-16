namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Geometria di presentazione in un riquadro 660 × 370, con uguale scala sui due assi.</summary>
public sealed record ShearFigure(double Left, double Top, double Width, double Height, double CoverPixels)
{
    public static ShearFigure Empty { get; } = new(100, 60, 0, 0, 0);
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double MidX => Left + Width / 2;
    public double MidY => Top + Height / 2;
    public double WidthEditorX => MidX - 52;
    public double WidthEditorY => Bottom + 24;
    public double HeightEditorX => Right + 35;
    public double HeightEditorY => MidY - 17;
    public double DimensionY => Bottom + 42;
    public double DimensionX => Right + 28;
    public double ReinforcementY => Bottom - CoverPixels;
    public double CoverLabelY => ReinforcementY - 22;
    public double DLabelY => Top + (Height - CoverPixels) / 2 - 9;
    public static ShearFigure Create(double b, double h, double cover)
    {
        // Riserva margini alle quote e usa un solo fattore: una parete sottile non viene deformata.
        double scale = Math.Min(340 / b, 240 / h);
        double width = b * scale, height = h * scale;
        return new(80 + (340 - width) / 2, 25 + (240 - height) / 2, width, height, cover * scale);
    }
}
