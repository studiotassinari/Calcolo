using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using STA.Calcolo.Desktop.Controls;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Views;
using Vento.Aerodinamica;

/// <summary>Vento sulle costruzioni: finestra reale WPF, ricalcolo a ogni modifica, colonne dinamiche, disegno e assenza di errori di binding.</summary>
internal static class WindBuildingSmoke
{
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (message is not null) Errors.Add(message); }
        public override void WriteLine(string? message) { if (message is not null) Errors.Add(message); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Vento costruzioni: " + message); }
    private static DataGridColumn Column(DataGrid grid, string header) => grid.Columns.Single(c => (c.Header as string)?.StartsWith(header) == true);

    public static void Run(Action<Window, string, double> render)
    {
        var listener = new BindingErrors(); var source = PresentationTraceSources.DataBindingSource; var previous = source.Switch.Level;
        source.Switch.Level = SourceLevels.Error; source.Listeners.Add(listener);
        var files = new MemoryFileService(); var vm = new WindBuildingViewModel(files); var window = new WindBuildingWindow { DataContext = vm };
        try
        {
            // Stato iniziale: falda doppia, risultati già presenti senza alcun comando.
            Check(vm.HasRows && vm.Rows.Count == 6, "calcolo iniziale mancante: " + vm.Status);
            render(window, "vento-costruzione", 1400);
            Check(window.FontFamily.Source.Contains("Acherus"), "font aziendale mancante");
            var grid = (DataGrid)window.FindName("ResultsGrid"); var drawing = (WindBuildingDrawing)window.FindName("Drawing");
            Check(grid.Items.Count == 6 && drawing.ActualWidth > 300 && drawing.ActualHeight > 200, "tabella o disegno non disposti");
            Check(Column(grid, "F [kN]").Visibility == Visibility.Collapsed, "colonna forza visibile per un edificio");

            // Ogni modifica della geometria aggiorna subito risultati e disegno.
            double before = vm.Rows.Single(r => r.Superficie == "Falda sottovento").Coefficiente;
            vm.Alpha = 30; window.UpdateLayout();
            Check(Math.Abs(before + .6) < 1e-12 && Math.Abs(vm.Rows.Single(r => r.Superficie == "Falda sottovento").Coefficiente + .45) < 1e-12, "ricalcolo dopo α");
            Check(drawing.Alpha == 30, "disegno non aggiornato");

            // Anche il vento del sito ricalcola: raddoppiando cd raddoppia la pressione esterna.
            double pressure = vm.Rows[0].Pressione; var cd = vm.SiteFields.Single(f => f.Key == "cd");
            cd.Value = "2"; Check(Math.Abs(vm.Rows[0].Pressione - 2 * pressure) < 1e-9, "ricalcolo dopo cd"); cd.Value = "1";

            // Pressione interna: compaiono due colonne con intestazione dinamica.
            vm.InternalCase = CasoPressioneInterna.PorositàUniforme; window.UpdateLayout();
            Check(vm.ShowNet1 && vm.ShowNet2 && vm.Rows.All(r => r.PressioneNetta1.HasValue && r.PressioneNetta2.HasValue), "pressioni nette mancanti");
            Check(grid.Columns.Count(c => c.Visibility == Visibility.Visible && (c.Header as string)?.StartsWith("p − pi") == true) == 2, "colonne nette non visibili");
            render(window, "vento-costruzione-pressione-interna", 1400);

            // Tettoia: spariscono pressione interna e direzione non pertinenti, compare la forza.
            vm.Type = TipologiaCostruzione.TettoiaFaldeMultiple; vm.Spans = 5; vm.D = 30; vm.H = 5; vm.Blockage = .5; window.UpdateLayout();
            Check(!vm.ShowInternal && vm.ShowBlockage && vm.ShowSpans && vm.Rows.Count == 6 && vm.Rows.All(r => r.Simbolo == "cF" && r.Forza.HasValue), "tettoia a falde multiple");
            Check(Column(grid, "F [kN]").Visibility == Visibility.Visible && !vm.ShowNet1, "colonne della tettoia");
            render(window, "vento-costruzione-tettoia", 1400);

            // Volta cilindrica: quota di riferimento h + f/2 e due valori nella zona A fra f/d = 0,2 e 0,3.
            vm.Type = TipologiaCostruzione.CoperturaVoltaCilindrica; vm.D = 16; vm.H = 6; vm.Rise = 4; window.UpdateLayout();
            Check(vm.ShowRise && !vm.ShowAlpha && vm.Rows.Count(r => r.Superficie.StartsWith("Volta, zona A")) == 2, "volta cilindrica");
            Check(Math.Abs(vm.Rows.Single(r => r.Superficie.StartsWith("Volta, zona C")).Ze - 8) < 1e-12, "ze della volta");
            render(window, "vento-costruzione-volta", 1400);

            // Caso di riferimento della volta ribassata: b = 90, d = 80, h = 5, f = 10 m (h/d = 0,0625, f/d = 0,125).
            // Valori attesi calcolati a mano dalla Fig. C3.3.19 (c): A = 0,20 + 0,125·(−1,20 − 0,20) = +0,025; B = −0,825; C = −0,40.
            vm.InternalCase = CasoPressioneInterna.NonConsiderata; vm.B = 90; vm.D = 80; vm.H = 5; vm.Rise = 10; window.UpdateLayout();
            double Cpe(string zona) => vm.Rows.Single(r => r.Superficie.StartsWith(zona)).Coefficiente;
            Check(Math.Abs(Cpe("Volta, zona A") - .025) < 1e-12 && Math.Abs(Cpe("Volta, zone B") + .825) < 1e-12 && Math.Abs(Cpe("Volta, zona C") + .4) < 1e-12, "coefficienti della volta ribassata");
            // Valutazioni mostrate: h, d, f, h/d, f/d e peso dell'interpolazione, come richiede la lettura del grafico.
            string Evaluation(string name) => vm.Evaluations.Single(v => v.Grandezza == name).Valore;
            Check(Evaluation("h/d") == "0,063" && Evaluation("f/d") == "0,125" && Evaluation("Peso dell'interpolazione su h/d") == "0,125", "valutazioni h/d e f/d: " + Evaluation("h/d"));
            Check(Evaluation("cpe,A per h/d = 0") == "+0,20" && Evaluation("cpe,A per h/d ≥ 0,5, ramo negativo") == "-1,20", "letture della zona A");
            // Quattro zone di uguale sviluppo: i tratti sono contigui e coprono tutta la corda.
            var zone = vm.Rows.Where(r => r.Superficie.StartsWith("Volta")).SelectMany(r => r.Tratti).ToList();
            Check(zone.Count == 3 && Math.Abs(zone[0].Lunghezza - zone[2].Lunghezza) < 1e-9 && Math.Abs(zone[1].Lunghezza - 2 * zone[0].Lunghezza) < 1e-9, "zone di uguale sviluppo");
            Check(Math.Abs(zone[0].XInizio) < 1e-9 && Math.Abs(zone[2].XFine - 80) < 1e-9 && Math.Abs(zone[1].XInizio - zone[0].XFine) < 1e-12, "zone contigue sulla corda");
            Check(vm.Rows.Single(r => r.Superficie.StartsWith("Volta, zona A")).Estensione.StartsWith("x = 0,00 ÷ "), "estensione in tabella");
            var tabs = (TabControl)window.FindName("FigureTabs");
            tabs.SelectedIndex = 1; window.UpdateLayout();
            var figura = (WindPressureDrawing)window.FindName("PressureDrawing");
            Check(figura.Rows!.Count == 6 && figura.ActualWidth > 300, "figura dei risultati non disposta");
            render(window, "vento-costruzione-figura-risultati", 1400);
            tabs.SelectedIndex = 2; window.UpdateLayout();
            Check(((DataGrid)window.FindName("EvaluationsGrid")).Items.Count == vm.Evaluations.Count && vm.Evaluations.Count >= 12, "scheda valutazioni");
            render(window, "vento-costruzione-valutazioni", 1400);
            tabs.SelectedIndex = 0;

            // Due condizioni di carico: la figura segue quella scelta.
            vm.Type = TipologiaCostruzione.CoperturaFaldaDoppia; vm.B = 20; vm.D = 12; vm.H = 8; vm.Alpha = 30; window.UpdateLayout();
            Check(vm.ShowConditions && vm.Conditions.SequenceEqual(new[] { "valori negativi", "valori positivi" }) && vm.SelectedCondition == "valori negativi", "condizioni di carico");
            Check(vm.FigureRows.Count == 5 && vm.FigureRows.Single(r => r.Superficie == "Falda sopravento").Coefficiente < 0, "figura: valori negativi");
            vm.SelectedCondition = "valori positivi"; Check(vm.FigureRows.Single(r => r.Superficie == "Falda sopravento").Coefficiente > 0, "figura: valori positivi");

            // Salva e riapri: i dati tornano identici; un file errato non altera il lavoro aperto.
            vm.InternalCase = CasoPressioneInterna.AperturaDominanteTripla; vm.DominantFace = FacciaEdificio.Laterale;
            vm.SiteFields.Single(f => f.Key == "quota").Value = "350";
            files.NextSave = "vento.json"; vm.SaveCommand.Execute(null); Check(files.Content.ContainsKey("vento.json") && vm.Status == "Progetto salvato.", "salvataggio: " + vm.Status);
            double salvata = vm.Rows[0].Pressione;
            vm.Type = TipologiaCostruzione.TettoiaFaldaSingola; vm.Alpha = 10; vm.SiteFields.Single(f => f.Key == "quota").Value = "0";
            files.NextOpen = "vento.json"; vm.OpenCommand.Execute(null); window.UpdateLayout();
            Check(vm.Type == TipologiaCostruzione.CoperturaFaldaDoppia && vm.Alpha == 30 && vm.InternalCase == CasoPressioneInterna.AperturaDominanteTripla && vm.DominantFace == FacciaEdificio.Laterale, "caricamento dei dati");
            Check(vm.SiteFields.Single(f => f.Key == "quota").Value == "350" && Math.Abs(vm.Rows[0].Pressione - salvata) < 1e-9 && vm.Status == "Progetto caricato.", "risultati dopo il caricamento");
            files.Content["rotto.json"] = files.Content["vento.json"].Replace("CoperturaFaldaDoppia", "Inesistente"); files.NextOpen = "rotto.json"; vm.OpenCommand.Execute(null);
            Check(vm.Type == TipologiaCostruzione.CoperturaFaldaDoppia && vm.HasRows && vm.Status.Contains("tipologia"), "file errato accettato: " + vm.Status);
            files.Content["altro.json"] = "{\"schemaVersion\":1,\"module\":\"neve\",\"fields\":{},\"data\":\"\"}"; files.NextOpen = "altro.json"; vm.OpenCommand.Execute(null);
            Check(vm.HasRows && vm.Status.Contains("non è un progetto"), "file di un altro modulo accettato");

            // Capitolo Word sul modello aziendale: solo il capitolo, con gli stili del modello.
            files.NextSave = "capitolo.docx"; vm.WordCommand.Execute(null); Check(vm.Status == "Capitolo Word creato.", "capitolo Word: " + vm.Status);
            var docx = files.Binary["capitolo.docx"];
            System.IO.Directory.CreateDirectory("artifacts/reports"); System.IO.File.WriteAllBytes("artifacts/reports/vento-costruzione-capitolo.docx", docx);
            using (var zip = new System.IO.Compression.ZipArchive(new System.IO.MemoryStream(docx)))
            {
                string Part(string name) { using var reader = new System.IO.StreamReader(zip.GetEntry(name)!.Open()); return reader.ReadToEnd(); }
                string body = Part("word/document.xml");
                Check(body.Contains("w:val=\"Titolo1\"") && body.Contains("Azione del vento") && body.Contains("w:val=\"Titolo2\"") && body.Contains("Grigliatabella"), "stili del modello non usati");
                Check(!body.Contains("Sommario") && !body.Contains("<w:sdt>") && !body.Contains("EMISSIONE"), "frontespizio o sommario presenti");
                Check(body.Contains("headerReference") && body.Contains("footerReference") && zip.GetEntry("word/header1.xml") != null && zip.GetEntry("word/styles.xml") != null, "intestazione o stili del modello mancanti");
                Check(Part("[Content_Types].xml").Contains("wordprocessingml.document.main+xml") && zip.Entries.Count(e => e.FullName.StartsWith("word/media/capitolo-figura")) == 3, "tipo documento o figure");
                Check(body.Contains("Falda sopravento") && body.Contains("valori positivi") && body.Contains("Distribuzione longitudinale delle zone"), "contenuto del capitolo");
            }
            vm.InternalCase = CasoPressioneInterna.NonConsiderata;

            // Fuori dal campo delle tabelle il calcolo prosegue e avvisa.
            vm.Type = TipologiaCostruzione.CoperturaPiana; vm.H = 100; vm.D = 10; window.UpdateLayout();
            Check(vm.HasWarnings && vm.Warnings.Contains("h/d > 5") && ((TextBlock)window.FindName("WarningsText")).IsVisible, "avvertenza di snellezza");

            // Dati non validi: nessun risultato parziale e motivo nella barra di stato.
            vm.B = 0; Check(!vm.HasRows && vm.Status.Contains("b, d e h"), "geometria nulla accettata");
            vm.B = 20; vm.H = 250; Check(!vm.HasRows && vm.Status.Length > 0, "quota oltre 200 m accettata");
            vm.H = 8; vm.SiteFields.Single(f => f.Key == "quota").Value = "abc"; Check(!vm.HasRows, "quota non numerica accettata");
            vm.SiteFields.Single(f => f.Key == "quota").Value = "100"; Check(vm.HasRows, "ripristino dopo errore");

            Check(listener.Errors.Count == 0, "errori di binding: " + string.Join(" | ", listener.Errors.Take(3)));
            Console.WriteLine("PASS WPF vento-costruzione: ricalcolo immediato, pressione interna, tettoie, volta, valutazioni, figura dei risultati, salva/apri, capitolo Word, avvertenze, dati non validi, binding");
        }
        finally { window.Close(); source.Listeners.Remove(listener); source.Switch.Level = previous; }
    }
}
