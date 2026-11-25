using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
namespace STA.Calcolo.Reporting;

/// <summary>
/// Scrive un capitolo in un .docx costruito sul modello Word aziendale (WordTemplate/TA-Template.dotx):
/// conserva stili, tema, numerazione dei titoli, intestazione e piè di pagina del modello e ne sostituisce
/// il contenuto con il solo capitolo. Frontespizio e sommario del modello non vengono riportati.
/// Misure di Word: larghezze in dxa (1/20 di punto), immagini in EMU (914 400 per pollice).
/// </summary>
public static class TemplateChapterWriter
{
    private const string TemplateResource = "STA.Calcolo.Reporting.WordTemplate.TA-Template.dotx";
    private const string DocumentPart = "word/document.xml", RelationsPart = "word/_rels/document.xml.rels", TypesPart = "[Content_Types].xml";
    private const string TemplateType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";
    private const string DocumentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string ImageRelation = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
    // A4 con i margini del modello: 11906 − 2·1134 dxa di larghezza utile, pari a 6 120 130 EMU.
    private const int TextWidth = 9638;
    private const long TextWidthEmu = 6120130;

    private static string E(string s) => SecurityElement.Escape(s) ?? "";
    private static string Run(string text, string properties = "") => $"<w:r>{(properties.Length > 0 ? "<w:rPr>" + properties + "</w:rPr>" : "")}<w:t xml:space=\"preserve\">{E(text)}</w:t></w:r>";

    public static byte[] Write(ReportChapter chapter)
    {
        using var template = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResource)
            ?? throw new InvalidOperationException("Modello Word aziendale non incorporato.");
        using var source = new ZipArchive(template, ZipArchiveMode.Read);
        string Text(string part) { using var reader = new StreamReader(source.GetEntry(part)!.Open(), Encoding.UTF8); return reader.ReadToEnd(); }

        var figures = chapter.Blocks.OfType<ChapterFigure>().ToList();
        string relations = Text(RelationsPart);
        // L'ultima sezione del modello eredita intestazione e piè di pagina dalla prima: qui vanno richiamati esplicitamente.
        string Reference(string kind)
        {
            var match = Regex.Match(relations, "<Relationship [^>]*Id=\"([^\"]+)\"[^>]*relationships/" + kind + "\"[^>]*/>");
            if (!match.Success) match = Regex.Match(relations, "<Relationship [^>]*relationships/" + kind + "\"[^>]*Id=\"([^\"]+)\"[^>]*/>");
            return match.Success ? $"<w:{kind}Reference w:type=\"default\" r:id=\"{match.Groups[1].Value}\"/>" : "";
        }
        string section = "<w:sectPr>" + Reference("header") + Reference("footer") +
            "<w:pgSz w:w=\"11906\" w:h=\"16838\" w:code=\"9\"/><w:pgMar w:top=\"1417\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/><w:cols w:space=\"708\"/><w:docGrid w:linePitch=\"360\"/></w:sectPr>";

        var body = new StringBuilder();
        body.Append(Heading(1, chapter.Title));
        int figure = 0;
        foreach (var block in chapter.Blocks)
        {
            switch (block)
            {
                case ChapterHeading h: body.Append(Heading(h.Level, h.Text)); break;
                case ChapterParagraph p: body.Append($"<w:p>{Run(p.Text)}</w:p>"); break;
                case ChapterBullet b: body.Append($"<w:p><w:pPr><w:pStyle w:val=\"Paragrafoelenco\"/></w:pPr>{Run(b.Text)}</w:p>"); break;
                case ChapterFormula f:
                    body.Append("<w:p><w:pPr><w:spacing w:before=\"60\" w:after=\"60\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><m:oMathPara><m:oMath>" + ReportMath.Omml(f.Formula) + "</m:oMath></m:oMathPara></w:p>");
                    if (f.Explanation.Length > 0) body.Append($"<w:p>{Run(f.Explanation)}</w:p>");
                    break;
                case ChapterTable t: body.Append(Table(t)); break;
                case ChapterFigure g: body.Append(Figure(++figure, g)); break;
            }
        }

        string document = Text(DocumentPart);
        int start = document.IndexOf("<w:body>", StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("Modello Word non valido: corpo del documento assente.");
        document = document[..start] + "<w:body>" + body + section + "</w:body></w:document>";

        string types = Text(TypesPart).Replace(TemplateType, DocumentType);
        if (!types.Contains("Extension=\"png\"")) types = types.Replace("<Default Extension=\"rels\"", "<Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"rels\"");
        relations = relations.Replace("</Relationships>", string.Concat(figures.Select((_, i) =>
            $"<Relationship Id=\"rIdCapitoloFigura{i + 1}\" Type=\"{ImageRelation}\" Target=\"media/capitolo-figura{i + 1}.png\"/>")) + "</Relationships>");

        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(text); }
            // [Content_Types].xml deve essere la prima voce del pacchetto.
            Entry(TypesPart, types);
            foreach (var entry in source.Entries)
            {
                if (entry.FullName is TypesPart or DocumentPart or RelationsPart) continue;
                using var input = entry.Open(); using var output = zip.CreateEntry(entry.FullName).Open(); input.CopyTo(output);
            }
            Entry(RelationsPart, relations);
            Entry(DocumentPart, document);
            for (int i = 0; i < figures.Count; i++) using (var stream = zip.CreateEntry($"word/media/capitolo-figura{i + 1}.png").Open()) stream.Write(figures[i].Png);
        }
        return memory.ToArray();
    }

    private static string Heading(int level, string text) => $"<w:p><w:pPr><w:pStyle w:val=\"Titolo{Math.Clamp(level, 1, 3)}\"/></w:pPr>{Run(text)}</w:p>";

    /// <summary>Paragrafo compatto per celle e didascalie: interlinea singola, senza spazio dopo.</summary>
    private static string Compact(string text, string align, string properties, int after = 0, bool keepNext = false) =>
        $"<w:p><w:pPr>{(keepNext ? "<w:keepNext/>" : "")}<w:spacing w:before=\"0\" w:after=\"{after}\" w:line=\"240\" w:lineRule=\"auto\"/><w:jc w:val=\"{align}\"/></w:pPr>{Run(text, properties)}</w:p>";

    private static string Table(ChapterTable table)
    {
        int columns = table.Headers.Length;
        double[] weights = table.Widths is { Length: > 0 } w && w.Length == columns ? w : Enumerable.Repeat(1d, columns).ToArray();
        double total = weights.Sum();
        int[] widths = weights.Select(x => (int)(TextWidth * x / total)).ToArray();
        var s = new StringBuilder();
        if (table.Caption.Length > 0) s.Append(Compact(table.Caption, "left", "<w:b/><w:sz w:val=\"20\"/>", 60, true));
        s.Append($"<w:tbl><w:tblPr><w:tblStyle w:val=\"Grigliatabella\"/><w:tblW w:w=\"{TextWidth}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblCellMar><w:top w:w=\"40\" w:type=\"dxa\"/><w:left w:w=\"70\" w:type=\"dxa\"/><w:bottom w:w=\"40\" w:type=\"dxa\"/><w:right w:w=\"70\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid>");
        foreach (int width in widths) s.Append($"<w:gridCol w:w=\"{width}\"/>");
        s.Append("</w:tblGrid>");
        void Row(string[] cells, bool header)
        {
            s.Append("<w:tr><w:trPr><w:cantSplit/>" + (header ? "<w:tblHeader/>" : "") + "</w:trPr>");
            for (int i = 0; i < columns; i++)
            {
                string align = i < table.TextColumns ? "left" : header ? "center" : "right";
                s.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{widths[i]}\" w:type=\"dxa\"/>{(header ? "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"E7E6E6\"/>" : "")}<w:vAlign w:val=\"center\"/></w:tcPr>");
                s.Append(Compact(i < cells.Length ? cells[i] : "", align, (header ? "<w:b/>" : "") + "<w:sz w:val=\"18\"/>") + "</w:tc>");
            }
            s.Append("</w:tr>");
        }
        Row(table.Headers, true);
        foreach (var row in table.Rows) Row(row, false);
        // Paragrafo vuoto dopo la tabella: separa dal testo che segue e impedisce a Word di unire due tabelle consecutive.
        return s.Append("</w:tbl>").Append(Compact("", "left", "<w:sz w:val=\"12\"/>", 120)).ToString();
    }

    private static string Figure(int number, ChapterFigure figure)
    {
        long width = (long)(TextWidthEmu * Math.Clamp(figure.Scale, .2, 1));
        long height = (long)(width / new ReportFigure("", figure.Png, "").AspectRatio);
        int id = 2000 + number;
        string drawing = $"<w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{width}\" cy=\"{height}\"/><wp:docPr id=\"{id}\" name=\"Figura {number}\"/><a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:nvPicPr><pic:cNvPr id=\"{id}\" name=\"capitolo-figura{number}.png\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rIdCapitoloFigura{number}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{width}\" cy=\"{height}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing>";
        return $"<w:p><w:pPr><w:keepNext/><w:spacing w:before=\"120\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/><w:jc w:val=\"center\"/></w:pPr><w:r>{drawing}</w:r></w:p>"
            + Compact(figure.Caption, "center", "<w:i/><w:sz w:val=\"18\"/>", 200);
    }
}
