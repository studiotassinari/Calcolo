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

    /// <summary>Rende il disegno in PNG con i livelli richiesti (forze, diagrammi, schema di calcolo).</summary>
    public static byte[] Render(WallGeometry geometry, WallDesignCase? designCase, bool forces = true, bool diagrams = true, bool scheme = false)
    {
        var drawing = new RetainingWallDrawing
        {
            Geometry = geometry, Forces = designCase?.Forces, Diagrams = designCase?.Diagrams,
            ShowForces = forces && designCase is not null, ShowDiagrams = diagrams && designCase is not null, ShowScheme = scheme
        };
        var size = new Size(FigureWidth, FigureHeight);
        drawing.Measure(size); drawing.Arrange(new Rect(size)); drawing.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(FigureWidth * Scale), (int)(FigureHeight * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }

    /// <summary>Figure della relazione, con le chiavi attese da RetainingWallReportBuilder.</summary>
    public static Dictionary<string, byte[]> Figures(WallDesignResult result) => new()
    {
        ["sezione"] = Render(result.Geometry, null, scheme: true),
        ["statica"] = Render(result.Geometry, result.StaticBase),
        ["ribaltamento"] = Render(result.Geometry, result.StaticOverturning, diagrams: false, scheme: true),
        ["pressione"] = Render(result.Geometry, result.StaticBase, forces: false),
        ["sisma"] = Render(result.Geometry, result.SeismicBase)
    };

    private static ReportDocument Build(WallDocument document, WallDesignResult result) =>
        RetainingWallReportBuilder.Build(document, result, Figures(result), DateTime.Now);

    public byte[] Word(WallDocument document, WallDesignResult result) => WordReportWriter.Write(Build(document, result), Logo());

    public byte[] Pdf(WallDocument document, WallDesignResult result)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return PdfReportWriter.Write(Build(document, result), Logo(), File.ReadAllBytes(Path.Combine(fonts, "arial.ttf")), File.ReadAllBytes(Path.Combine(fonts, "arialbd.ttf")));
    }
}
