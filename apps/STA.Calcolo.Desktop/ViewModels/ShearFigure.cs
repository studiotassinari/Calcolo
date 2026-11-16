namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Presentation geometry in a 660 × 470 viewport, with equal scale on both axes.</summary>
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
        double scale = Math.Min(360 / b, 310 / h);
        double width = b * scale, height = h * scale;
        return new(80 + (360 - width) / 2, 44 + (310 - height) / 2, width, height, cover * scale);
    }
}
