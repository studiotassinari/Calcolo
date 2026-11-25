using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Controls;
using STA.Calcolo.Reporting;
using Vento.Aerodinamica;
namespace STA.Calcolo.Desktop.Services;

/// <summary>Contratto dell'esportazione Word del vento sulle costruzioni: il ViewModel non dipende da WPF né dagli scrittori.</summary>
public interface IWindBuildingReportService
{
    byte[] Word(WindBuildingInput input, WindBuildingResult result);
}

/// <summary>
/// Produce le figure del capitolo disegnando fuori schermo gli stessi controlli della finestra (schema quotato e
/// figura delle pressioni, una per condizione di carico) e delega testo e impaginazione a STA.Calcolo.Reporting,
/// che scrive il capitolo sul modello Word aziendale. Le figure sono rese a 3× per restare leggibili in stampa.
/// </summary>
public sealed class WindBuildingReportService : IWindBuildingReportService
{
    private const double FigureWidth = 900, SchemeHeight = 330, PressureHeight = 400, Scale = 3;

    /// <summary>Rende in PNG un disegno del vento dopo avergli assegnato la geometria.</summary>
    public static byte[] Render(WindBuildingDrawing drawing, GeometriaCostruzione g, double height)
    {
        drawing.Type = g.Tipologia; drawing.Direction = g.Direzione; drawing.MultiScheme = g.SchemaMultiplo;
        drawing.B = g.B; drawing.D = g.D; drawing.H = g.H; drawing.Alpha = g.Alfa; drawing.Rise = g.Freccia; drawing.Spans = g.NumeroTratti;
        var size = new Size(FigureWidth, height);
        drawing.Measure(size); drawing.Arrange(new Rect(size)); drawing.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(FigureWidth * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }

    /// <summary>Dati del capitolo: righe del sito con le etichette della finestra e figure per ogni condizione di carico.</summary>
    public static WindBuildingReportData Data(WindBuildingInput input, WindBuildingResult result)
    {
        var g = input.Geometry; var actions = result.Actions;
        // Con il modello NTC 2018 la fascia costiera non entra nel calcolo e non va riportata; con quello storico vale l'opposto per la categoria manuale.
        bool legacy = input.Site["modello"] == WindWorkspace.Legacy;
        var site = WindBuildingProject.SiteFields.Where(f => f.Key != (legacy ? "categoria" : "distanza"))
            .Select(f => (f.Label, input.Site[f.Key])).ToList();
        IReadOnlyList<string> conditions = actions.Condizioni.Count > 0 ? actions.Condizioni : [""];
        var pressures = conditions.Select(c => (c, Render(new WindPressureDrawing { Rows = actions.PerCondizione(c), Caption = c.Length > 0 ? "Condizione: " + c : "" }, g, PressureHeight))).ToList();
        return new(site, result.Site.Velocity, new(result.Site.Qr, result.Site.Category, result.Site.Ct, result.Site.Cd), g,
            input.InternalCase, input.DominantFace, actions, Render(new WindBuildingDrawing(), g, SchemeHeight), pressures);
    }

    public byte[] Word(WindBuildingInput input, WindBuildingResult result) => TemplateChapterWriter.Write(WindBuildingReportBuilder.Build(Data(input, result)));
}
