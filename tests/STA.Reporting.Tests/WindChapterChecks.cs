using System.IO.Compression;
using System.Xml.Linq;
using STA.Calcolo.Reporting;
using Vento.Aerodinamica;

/// <summary>
/// Capitolo del vento sul modello Word aziendale: il documento deve usare gli stili del modello,
/// non contenere frontespizio né sommario, restare XML ben formato e riportare valutazioni, zone e figure.
/// </summary>
internal static class WindChapterChecks
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception("Capitolo vento: " + message); }

    public static void Run(byte[] image)
    {
        // Volta ribassata: b = 90, d = 80, h = 5, f = 10 m; vento con qr = 390,625 N/m², categoria III.
        var geometry = new GeometriaCostruzione { Tipologia = TipologiaCostruzione.CoperturaVoltaCilindrica, B = 90, D = 80, H = 5, Freccia = 10 };
        var wind = new DatiVentoSito(390.625, "III", 1, 1);
        var actions = CalcolatoreAzioni.Calcola(wind, geometry, CasoPressioneInterna.PorositàUniforme, FacciaEdificio.Sopravento);
        var data = new WindBuildingReportData([("Zona geografica", "Zona2"), ("Quota sito [m s.l.m.]", "100")], 25, wind, geometry,
            CasoPressioneInterna.PorositàUniforme, FacciaEdificio.Sopravento, actions, image, [("", image)]);
        var chapter = WindBuildingReportBuilder.Build(data);
        Assert(chapter.Title == "Azione del vento" && chapter.Blocks.OfType<ChapterHeading>().All(h => h.Level == 2), "struttura dei titoli");
        Assert(chapter.Blocks.OfType<ChapterFigure>().Count() == 2 && chapter.Blocks.OfType<ChapterTable>().Count() == 5, "figure e tabelle del capitolo");
        var evaluations = chapter.Blocks.OfType<ChapterTable>().Single(t => t.Caption.StartsWith("Valutazioni"));
        Assert(evaluations.Rows.Any(r => r[0] == "h/d" && r[2] == "0,063") && evaluations.Rows.Any(r => r[0] == "f/d" && r[2] == "0,125"), "valutazioni h/d e f/d");
        var zones = chapter.Blocks.OfType<ChapterTable>().Single(t => t.Caption.StartsWith("Estensione"));
        Assert(zones.Rows.Count == 3 && zones.Rows[0][1] == "0,00" && zones.Rows[2][2] == "80,00" && zones.Rows[0][5] == zones.Rows[2][5], "zone di uguale sviluppo");

        var docx = TemplateChapterWriter.Write(chapter);
        using var zip = new ZipArchive(new MemoryStream(docx));
        Assert(zip.Entries[0].FullName == "[Content_Types].xml", "ordine delle parti del pacchetto");
        string Part(string name) { using var reader = new StreamReader(zip.GetEntry(name)!.Open()); return reader.ReadToEnd(); }
        // Ogni parte XML scritta deve essere ben formata, altrimenti Word rifiuta il file.
        var document = XDocument.Parse(Part("word/document.xml")); XDocument.Parse(Part("[Content_Types].xml")); XDocument.Parse(Part("word/_rels/document.xml.rels"));
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var styles = document.Descendants(w + "pStyle").Select(e => (string)e.Attribute(w + "val")!).ToList();
        Assert(styles.Count(s => s == "Titolo1") == 1 && styles.Count(s => s == "Titolo2") == chapter.Blocks.OfType<ChapterHeading>().Count() && styles.Contains("Paragrafoelenco"), "stili del modello");
        Assert(document.Descendants(w + "tblStyle").All(e => (string)e.Attribute(w + "val")! == "Grigliatabella") && document.Descendants(w + "tbl").Count() == 5, "stile delle tabelle");
        Assert(!document.Descendants(w + "sdt").Any() && !document.ToString().Contains("Sommario") && !document.ToString().Contains("EMISSIONE"), "frontespizio o sommario presenti");
        var section = document.Descendants(w + "sectPr").Single();
        Assert(section.Elements(w + "headerReference").Any() && section.Elements(w + "footerReference").Any(), "intestazione e piè di pagina del modello");
        Assert(Part("[Content_Types].xml").Contains("wordprocessingml.document.main+xml") && !Part("[Content_Types].xml").Contains("template.main"), "tipo di documento");
        Assert(zip.GetEntry("word/styles.xml") != null && zip.GetEntry("word/numbering.xml") != null && zip.GetEntry("word/header1.xml") != null && zip.GetEntry("word/footer1.xml") != null, "parti del modello");
        Assert(zip.Entries.Count(e => e.FullName.StartsWith("word/media/capitolo-figura")) == 2 && Part("word/_rels/document.xml.rels").Contains("rIdCapitoloFigura2"), "figure incorporate");
        Console.WriteLine("PASS capitolo vento: modello Word aziendale, solo capitolo, stili, valutazioni, zone, figure e XML ben formato");
    }
}
