using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using STA.Calcolo.Desktop.Controls;
using STA.Calcolo.Reporting;
using STA.MuroSostegno.Core;
namespace STA.Calcolo.Desktop.Services;

/// <summary>Contratto dell'export della relazione del muro: il ViewModel non dipende da WPF né dagli scrittori.</summary>
public interface IRetainingWallReportService
{
    byte[] Word(WallDocument document, WallDesignResult result);
    byte[] Pdf(WallDocument document, WallDesignResult result);
}

/// <summary>
/// Produce le figure della relazione disegnando fuori schermo lo stesso controllo della finestra
/// (sezione, combinazione statica A1, combinazione sismica più gravosa) e delega impaginazione e formule
/// a STA.Calcolo.Reporting. Le figure sono generate a 820×358 unità logiche con risoluzione 3×,
/// così i testi del disegno restano leggibili alla larghezza della pagina A4.
/// </summary>
public sealed class RetainingWallReportService : IRetainingWallReportService
{
    private const double FigureWidth = 820, FigureHeight = 358, Scale = 3;

    private static byte[] Logo()
    {
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/STA.Calcolo.Desktop;component/Assets/Brand/Logo.png"))!.Stream;
        using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
    }

    /// <summary>Rende il disegno in PNG; forze e diagrammi assenti per la sola sezione geometrica.</summary>
    public static byte[] Render(WallGeometry geometry, WallDesignCase? designCase)
    {
        var drawing = new RetainingWallDrawing
        {
            Geometry = geometry, Forces = designCase?.Forces, Diagrams = designCase?.Diagrams,
            ShowForces = designCase is not null, ShowDiagrams = designCase is not null
        };
        var size = new Size(FigureWidth, FigureHeight);
        drawing.Measure(size); drawing.Arrange(new Rect(size)); drawing.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(FigureWidth * Scale), (int)(FigureHeight * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }

    private static ReportDocument Build(WallDocument document, WallDesignResult result)
    {
        var figures = new Dictionary<string, byte[]>
        {
            ["sezione"] = Render(result.Geometry, null),
            ["statica"] = Render(result.Geometry, result.StaticBase),
            ["sisma"] = Render(result.Geometry, result.SeismicBase)
        };
        return RetainingWallReportBuilder.Build(document, result, figures, DateTime.Now);
    }

    public byte[] Word(WallDocument document, WallDesignResult result) => WordReportWriter.Write(Build(document, result), Logo());

    public byte[] Pdf(WallDocument document, WallDesignResult result)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return PdfReportWriter.Write(Build(document, result), Logo(), File.ReadAllBytes(Path.Combine(fonts, "arial.ttf")), File.ReadAllBytes(Path.Combine(fonts, "arialbd.ttf")));
    }
}
