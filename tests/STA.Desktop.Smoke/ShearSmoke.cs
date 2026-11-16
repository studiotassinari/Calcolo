using System.Diagnostics;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Views;

// Test su finestre reali: verifica binding, editor delle quote, invalidazione ed esportazioni.
internal static class ShearSmoke
{
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (message != null) Errors.Add(message); }
        public override void WriteLine(string? message) { if (message != null) Errors.Add(message); }
    }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(Action<Window,string,double> render)
    {
        // Gli errori di binding devono fallire il test, non restare messaggi nascosti nel debugger.
        var listener = new BindingErrors();
        var source = PresentationTraceSources.DataBindingSource;
        var previous = source.Switch.Level;
        source.Switch.Level = SourceLevels.Error; source.Listeners.Add(listener);
        var files = new MemoryFileService();
        var vm = new ShearViewModel(files);
        var window = new ShearWindow { DataContext = vm };
        try
        {
            vm.Kind = "Parete"; vm.ByKey["H"].Value = "2000"; vm.ByKey["B"].Value = "250";
            vm.CalculateCommand.Execute(null);
            Assert(vm.HasResults, vm.Status);
            render(window, "taglio-parete", 1320);
            // Protegge l’applicazione esplicita del tema alle finestre derivate da Window.
            Assert(window.FontFamily.Source.Contains("Acherus") && window.Background != null, "Tema aziendale non applicato");
            Assert(((Panel)window.FindName("ShearLayout")).Background != null, "Sfondo anteprima trasparente");
            var figure = (ShearSectionView)window.FindName("SectionFigure");
            var width = (TextBox)figure.FindName("FigureWidthEditor");
            var height = (TextBox)figure.FindName("FigureHeightEditor");
            Assert(width.IsVisible && height.IsVisible, "Figure editors hidden");
            width.Text = "250,5"; width.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert(vm.ByKey["B"].Value == "250,5" && !vm.HasResults && !vm.WordCommand.CanExecute(null), "Quote did not update/invalidate");
            Assert(Math.Abs(vm.Figure.Width / vm.Figure.Height - 250.5/2000) < 1e-10, "Figure has distorted axes");
            // L’utente deve poter svuotare la quota senza perdere il focus sul controllo.
            height.Focus(); height.Text = ""; height.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); window.UpdateLayout();
            Assert(height.IsVisible && height.IsKeyboardFocused && !vm.HasFigure && !vm.CalculateCommand.CanExecute(null), "Clearing a quote hid the editor or accepted invalid input");
            height.Text = "2100"; height.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); window.UpdateLayout();
            Assert(vm.ByKey["H"].Value == "2100" && vm.HasFigure, "Quote cannot be re-entered");
            vm.ByKey["H"].Value = "1800"; window.UpdateLayout();
            Assert(height.Text == "1800", "Form to figure binding failed");
            vm.CalculateCommand.Execute(null); Assert(vm.HasResults, vm.Status);
            files.NextSave = "shear.json"; vm.SaveCommand.Execute(null);
            var saved = files.Content["shear.json"];
            files.NextSave = "shear-result.json"; vm.ExportCommand.Execute(null);
            Assert(files.Content["shear-result.json"].Contains("capacity"), "Shear JSON export failed");
            files.NextSave = "shear.docx"; vm.WordCommand.Execute(null);
            Assert(files.Binary.ContainsKey("shear.docx"), vm.Status);
            using (var zip = new ZipArchive(new System.IO.MemoryStream(files.Binary["shear.docx"])))
            using (var reader = new System.IO.StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            {
                var xml = reader.ReadToEnd();
                Assert(xml.Contains("Taglio") && xml.Contains("1.800"), "Word missing current geometry");
            }
            vm.Standard = "EN 1992"; Assert(vm.ByKey["AlphaCc"].Value == "1" && !vm.HasResults, "Standard change");
            files.NextOpen = "shear.json"; vm.OpenCommand.Execute(null);
            Assert(ShearFiles.Write(vm.Document()) == saved && !vm.HasResults, "Shear round trip");
            files.Content["broken.json"] = "{}"; files.NextOpen = "broken.json"; vm.OpenCommand.Execute(null);
            Assert(ShearFiles.Write(vm.Document()) == saved, "Invalid import modified open project");
            vm.ByKey["H"].Value = "40"; vm.ByKey["Cover"].Value = "40"; vm.CalculateCommand.Execute(null);
            Assert(!vm.HasResults && !vm.ExportCommand.CanExecute(null), "Invalid d accepted");
            vm.Load(new ShearWorkspace().Example()); vm.CalculateCommand.Execute(null);
            render(window, "taglio-trave", 1320);
            render(window, "taglio-compatto", 920);
            ((TabControl)window.FindName("DetailTabs")).SelectedIndex = 1;
            render(window, "taglio-risultati", 1180);
            vm.ByKey["V"].Value = "not a number"; Assert(!vm.HasResults && !vm.SaveCommand.CanExecute(null), "Invalid input commands enabled");
            Assert(listener.Errors.Count == 0, string.Join("\n", listener.Errors));
        }
        finally { window.Close(); source.Listeners.Remove(listener); source.Switch.Level = previous; }
        Console.WriteLine("PASS taglio WPF: quote bidirezionali, focus, invalidazione, JSON, Word, errori binding e layout");
    }
}
