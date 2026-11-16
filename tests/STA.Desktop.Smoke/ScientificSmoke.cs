using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Services;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Views;

/// <summary>Prove MVVM sulle viste effettive; il servizio cartografico è sostituito per non scaricare tessere in CI.</summary>
internal static class ScientificSmoke
{
    private sealed class Errors : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? m) { if (m != null) Messages.Add(m); }
        public override void WriteLine(string? m) { if (m != null) Messages.Add(m); }
    }
    private sealed class OfflineMap : IMapTileService
    {
        public Task<byte[]> ReadAsync(int z, int x, int y, CancellationToken token) => throw new System.IO.IOException("Offline simulato");
    }
    private static void Assert(bool ok, string text) { if (!ok) throw new Exception(text); }
    public static void Run(Action<Window,string,double> render)
    {
        var errors = new Errors(); var trace = PresentationTraceSources.DataBindingSource; var level = trace.Switch.Level;
        trace.Switch.Level = SourceLevels.Error; trace.Listeners.Add(errors);
        try
        {
            foreach (string id in new[] { "neve", "vento", "sisma", "calcestruzzo" })
            {
                var files = new MemoryFileService(); var vm = new ScientificViewModel(id, files);
                var view = new ScientificWindow { DataContext = vm }; var map = (GeographicMapView)view.FindName("SiteMap"); map.TileService = new OfflineMap();
                try
                {
                    vm.CalculateCommand.Execute(null); Assert(vm.HasResults, vm.Status);
                    render(view, "scientifico-" + id, 1280);
                    Assert(view.FontFamily.Source.Contains("Acherus"), "Tema non applicato");
                    files.NextSave = id + ".json"; vm.SaveCommand.Execute(null); string saved = files.Content[id + ".json"];
                    vm.Fields[0].Value += " errato"; Assert(!vm.HasResults && !vm.ExportCommand.CanExecute(null), "Risultati obsoleti");
                    files.NextOpen = id + ".json"; vm.OpenCommand.Execute(null); Assert(ModuleFiles.Write(vm.Document()) == saved, "Roundtrip");
                    files.Content["bad.json"] = "{}"; files.NextOpen = "bad.json"; vm.OpenCommand.Execute(null); Assert(ModuleFiles.Write(vm.Document()) == saved, "Importazione non atomica");
                    vm.CalculateCommand.Execute(null); Assert(vm.HasResults, vm.Status);
                    if (id == "sisma")
                    {
                        Assert(vm.ByKey["q"].Value == "1.5", "q iniziale");
                        ((TabItem)view.FindName("MapTab")).IsSelected = true; render(view, "sisma-mappa-offline", 1280);
                        var original = vm.Location;
                        map.SelectPixel(map.ActualWidth / 2 + 10, map.ActualHeight / 2);
                        Assert(vm.Location.Longitude > original.Longitude && !vm.HasResults && !vm.ExportSpectraCommand.CanExecute(null), "Marker non trasferito");
                        Assert(Math.Abs(ModuleInput.Number(vm.ByKey["lon"].Value) - vm.Location.Longitude) < .000001, "Ordine coordinate errato");
                        vm.ByKey["lat"].Value = "45,46"; view.UpdateLayout(); Assert(Math.Abs(map.Location.Latitude - 45.46) < .000001, "Testo non trasferito al marker");
                        int zoom = map.Zoom; map.ZoomAt(1,map.ActualWidth/2,map.ActualHeight/2); Assert(map.Zoom == zoom+1,"Zoom");
                        vm.ByKey["lat"].Value = "44.41778"; vm.ByKey["lon"].Value = "12.19944"; vm.CalculateCommand.Execute(null);
                        files.NextSave = "spettri.zip"; vm.ExportSpectraCommand.Execute(null); Assert(files.Binary.ContainsKey("spettri.zip"), vm.Status);
                    }
                    if (id == "neve") { vm.ByKey["provincia"].Value = "Ravenna"; vm.ByKey["falda"].Value = "45"; vm.CalculateCommand.Execute(null); Assert(vm.HasResults && vm.RoofAngle == 45 && vm.Context.Contains("Mediterranea"), vm.Status); }
                    if (id == "vento") { vm.ByKey["cp"].Value = "-0.8"; vm.CalculateCommand.Execute(null); Assert(vm.Series[0].Points.All(p => p.X < 0) && vm.ExposureSeries.Count == 5, "Grafici vento"); render(view,"vento-depressione",1280); }
                    ((TabItem)view.FindName("ResultsTab")).IsSelected = true; render(view, "scientifico-" + id + "-risultati", 1080);
                    if (id == "calcestruzzo")
                    {
                        Assert(vm.Results.All(r => r.Equazione.Contains("=") && r.Riferimento != null), "Formule o fonti mancanti");
                        // I vecchi file acquisiscono le ipotesi esplicite senza errori di binding.
                        vm.Load(new ModuleDocument(1, "calcestruzzo", new() { ["classe"]="C30_37" }, ""));
                        Assert(vm.ByKey["riciclo"].Value=="No", "Migrazione materiale");
                        vm.ByKey["vapore"].Value="Sì"; vm.CalculateCommand.Execute(null);
                        Assert(!vm.HasResults && vm.Status.Contains("vapore"), "Formula Ecm applicata fuori ambito");
                        vm.ByKey["ecm"].Value="31000"; vm.CalculateCommand.Execute(null);
                        Assert(vm.HasResults && vm.Results.Single(r=>r.Grandezza=="Ecm").Valore==31000, vm.Status);
                    }
                    render(view, "scientifico-" + id + "-compatto", 920);
                }
                finally { view.Close(); }
            }
            Assert(errors.Messages.Count == 0, string.Join("\n",errors.Messages));
        }
        finally { trace.Listeners.Remove(errors); trace.Switch.Level = level; }
        Console.WriteLine("PASS viste scientifiche: binding, figure, marker, esportazione, formule e invalidazione");
    }
}
