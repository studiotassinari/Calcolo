using System.Globalization;
using System.IO.Compression;
using System.Text;
namespace STA.Calcolo.Application;

/// <summary>Esporta tutte le serie del motore, non soltanto quelle selezionate o visibili nel grafico.</summary>
public static class SeismicTextExport
{
    public static IReadOnlyDictionary<string, string> Files(ModuleDocument doc)
    {
        var model = new SeismicWorkspace().Build(doc);
        var files = new Dictionary<string, string>();
        string F(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        void Add(STA.Carichi.Sisma.spettro spectrum, bool vertical, string kind)
        {
            string direction = vertical ? "V" : "H";
            var text = new StringBuilder();
            text.AppendLine("# TA-Calcolo — " + kind + " " + spectrum.statoLimite + " " + direction);
            text.AppendLine("# WGS84 lon=" + F(ModuleInput.Number(doc.Fields["lon"])) + " lat=" + F(ModuleInput.Number(doc.Fields["lat"])));
            text.AppendLine("# q_orizzontale_progetto=" + F(model.FattoreStruttura) + "; smorzamento=5%; modello storico");
            text.AppendLine("# T [s]\tSa [m/s^2]");
            // Tutti i campioni del motore sono inclusi, anche oltre il limite di visualizzazione.
            foreach (var v in spectrum.valoriSpettrali.OrderBy(v => v.periodo))
            {
                double acceleration = vertical ? v.rispostaVert : v.risposta;
                if (!double.IsFinite(v.periodo) || !double.IsFinite(acceleration)) throw new ArgumentException("Spettro non esportabile.");
                text.AppendLine(F(v.periodo) + "\t" + F(acceleration));
            }
            files.Add(kind + "_" + spectrum.statoLimite + "_" + direction + ".txt", text.ToString());
        }
        foreach (var spectrum in model.spettri) { Add(spectrum, false, "Elastico"); Add(spectrum, true, "Elastico"); }
        // Il motore esistente definisce lo spettro di progetto orizzontale solo allo SLV.
        // La risposta verticale non è ridotta con q: non viene etichettata come spettro di progetto.
        foreach (var spectrum in model.spettriProgetto) Add(spectrum, false, "Progetto");
        files.Add("LEGGIMI.txt", "Spettri elastici SLO, SLD, SLV, SLC nelle direzioni H e V; progetto SLV orizzontale.\n" +
            "Colonne separate da TAB, punto decimale, periodo in secondi e accelerazione in m/s².\n" +
            "Tutti i campioni nativi del motore storico, indipendentemente dal periodo massimo del grafico.\n" +
            "q riduce solo lo spettro orizzontale di progetto; non modifica gli elastici né la risposta verticale.\n");
        files.Add("progetto.json", ModuleFiles.Write(doc));
        return files;
    }
    /// <summary>Un unico salvataggio atomico per evitare cartelle di spettri parzialmente aggiornate.</summary>
    public static byte[] Zip(ModuleDocument doc)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var file in Files(doc))
            { using var writer = new StreamWriter(zip.CreateEntry(file.Key).Open(), new UTF8Encoding(false)); writer.Write(file.Value); }
        return output.ToArray();
    }
}
