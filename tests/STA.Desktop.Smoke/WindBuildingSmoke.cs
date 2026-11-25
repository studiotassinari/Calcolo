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
        var vm = new WindBuildingViewModel(); var window = new WindBuildingWindow { DataContext = vm };
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

            // Fuori dal campo delle tabelle il calcolo prosegue e avvisa.
            vm.Type = TipologiaCostruzione.CoperturaPiana; vm.H = 100; vm.D = 10; window.UpdateLayout();
            Check(vm.HasWarnings && vm.Warnings.Contains("h/d > 5") && ((TextBlock)window.FindName("WarningsText")).IsVisible, "avvertenza di snellezza");

            // Dati non validi: nessun risultato parziale e motivo nella barra di stato.
            vm.B = 0; Check(!vm.HasRows && vm.Status.Contains("b, d e h"), "geometria nulla accettata");
            vm.B = 20; vm.H = 250; Check(!vm.HasRows && vm.Status.Length > 0, "quota oltre 200 m accettata");
            vm.H = 8; vm.SiteFields.Single(f => f.Key == "quota").Value = "abc"; Check(!vm.HasRows, "quota non numerica accettata");
            vm.SiteFields.Single(f => f.Key == "quota").Value = "100"; Check(vm.HasRows, "ripristino dopo errore");

            Check(listener.Errors.Count == 0, "errori di binding: " + string.Join(" | ", listener.Errors.Take(3)));
            Console.WriteLine("PASS WPF vento-costruzione: ricalcolo immediato, pressione interna, tettoie, volta, avvertenze, dati non validi, binding");
        }
        finally { window.Close(); source.Listeners.Remove(listener); source.Switch.Level = previous; }
    }
}
