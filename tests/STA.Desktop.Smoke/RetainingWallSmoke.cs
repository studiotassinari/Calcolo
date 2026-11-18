using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Views;
using STA.MuroSostegno.Core;

/// <summary>Test del flusso utente: campi, calcolo, file, risultati obsoleti, navigazione e layout reale WPF.</summary>
internal static class RetainingWallSmoke
{
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors {get;}=[];
        public override void Write(string? message){if(message is not null)Errors.Add(message);}
        public override void WriteLine(string? message){if(message is not null)Errors.Add(message);}
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(Action<Window,string,double> render)
    {
        var listener=new BindingErrors();var source=PresentationTraceSources.DataBindingSource;var previous=source.Switch.Level;
        source.Switch.Level=SourceLevels.Error;source.Listeners.Add(listener);
        var files=new MemoryFileService();var vm=new RetainingWallViewModel(files);var window=new RetainingWallWindow{DataContext=vm};
        try
        {
            Check(vm.InputsValid&&vm.Geometry is not null&&vm.HasResults,"Geometria o calcolo iniziale mancante");
            vm.CalculateCommand.Execute(null);Check(vm.HasResults,vm.Status);
            render(window,"muro-sostegno",1460);
            Check(window.FontFamily.Source.Contains("Acherus"),"Font aziendale mancante");
            files.NextSave="wall.json";vm.SaveCommand.Execute(null);var saved=files.Content["wall.json"];
            files.NextSave="wall.mdst";vm.SaveMdstCommand.Execute(null);Check(files.Content["wall.mdst"].Contains("Xd=4"),"Salvataggio MDST mancante");
            files.NextSave="wall.txt";vm.ExportCommand.Execute(null);Check(files.Content["wall.txt"].Contains("NOTE E LIMITI"),"Esportazione senza limiti");
            files.NextSave="wall-results.json";vm.ExportJsonCommand.Execute(null);Check(files.Content["wall-results.json"].Contains("slidingThrust"),"Esportazione JSON mancante");
            // MVVM: ogni modifica valida ricalcola subito i risultati coerenti con la nuova geometria.
            vm.ByKey["H1"].Value="6,5";Check(vm.HasResults&&vm.ExportCommand.CanExecute(null)&&vm.Geometry is not null,"Risultati non ricalcolati dopo modifica");
            Check(Math.Abs(vm.Geometry!.Wall[4].Y-7.1)<1e-10&&Math.Abs(vm.Result!.Geometry.Wall[4].Y-7.1)<1e-10,"Disegno o risultati non aggiornati");
            Check(vm.Summary.Count>0&&vm.Results.Count>vm.Summary.Count,"Sintesi non aggiornata");
            // Motore rivisto predefinito; l'originale resta selezionabile e ricalcolato.
            Check(vm.Engine==WallEngine.Rivisto&&vm.Design is not null&&vm.Checks.Count==2&&vm.Pressures is not null,"Motore rivisto non attivo");
            vm.Engine=WallEngine.Originale;Check(vm.HasResults&&vm.Checks.Count==1&&vm.Notes.Contains(WallCalculator.Scope),"Motore originale non selezionabile");
            vm.Engine=WallEngine.Rivisto;Check(vm.HasResults&&vm.Notes.Contains(WallDesignCalculator.Scope),"Ritorno al motore rivisto non riuscito");
            // Forze e diagrammi della combinazione disegnata; relazione con figure prodotte dal controllo WPF.
            Check(vm.DrawingForces is {Count:>3}&&vm.DrawingDiagrams is not null&&vm.DrawingCases.Count==5,"Forze o diagrammi assenti");
            vm.DrawingCase=vm.DrawingCases[1];Check(vm.DrawingDiagrams!.Base.N>0,"Combinazione del disegno non applicata");
            files.NextSave="wall-report.pdf";vm.PdfReportCommand.Execute(null);Check(files.Binary.TryGetValue("wall-report.pdf",out var pdf)&&pdf.Length>50000,"Relazione PDF mancante: "+vm.Status);
            files.NextSave="wall-report.docx";vm.WordReportCommand.Execute(null);Check(files.Binary.TryGetValue("wall-report.docx",out var docx)&&docx.Length>50000,"Relazione Word mancante: "+vm.Status);
            // Figure reali della relazione salvate fra gli artefatti della CI per il controllo visivo.
            System.IO.Directory.CreateDirectory("artifacts");
            foreach(var (key,png) in STA.Calcolo.Desktop.Services.RetainingWallReportService.Figures(vm.Design!))
            {Check(png.Length>20000,"Figura della relazione vuota: "+key);System.IO.File.WriteAllBytes($"artifacts/muro-figura-{key}.png",png);}
            // Grip: trascinamento della sommità a quota 7,62 m → H1 = 7,6 − H2 = 7 m con passo 5 cm.
            vm.GripCommand.Execute(new WallGripDrag("H1",new(1,7.62),.05));Check(vm.ByKey["H1"].Value=="7"&&vm.HasResults&&Math.Abs(vm.Result!.Geometry.Wall[4].Y-7.6)<1e-10,"Grip H1 non applicato");
            vm.GripCommand.Execute(new WallGripDrag("Bf",new(.5,0),.05));Check(vm.ByKey["Bf"].Value=="4,5"&&vm.HasResults,"Grip non ammesso ha modificato il progetto");
            vm.ByKey["H1"].Value="6,5";
            files.NextOpen="wall.json";vm.OpenCommand.Execute(null);Check(WallFiles.Write(vm.Document())==saved&&vm.HasResults,"Ripristino JSON errato");
            files.Content["broken.mdst"]="H1=5";files.NextOpen="broken.mdst";vm.OpenCommand.Execute(null);
            Check(WallFiles.Write(vm.Document())==saved,"Import invalido ha modificato il progetto");
            files.NextOpen="wall.mdst";vm.OpenCommand.Execute(null);Check(vm.InputsValid&&vm.HasResults,"Import MDST errato");
            vm.ByKey["H1"].Value="";Check(vm.Geometry is null&&!vm.CalculateCommand.CanExecute(null)&&!vm.SaveCommand.CanExecute(null),"Input vuoto accettato");
            vm.ExampleCommand.Execute(null);vm.CalculateCommand.Execute(null);render(window,"muro-sostegno-compatto",980);
            ((TabControl)window.FindName("WallTabs")).SelectedIndex=1;render(window,"muro-sostegno-risultati",1460);
            Check(((DataGrid)window.FindName("ResultsGrid")).Columns[0].ActualWidth>=220,"Colonna delle grandezze non leggibile");
            ((TabControl)window.FindName("WallTabs")).SelectedIndex=2;render(window,"muro-sostegno-note",1460);
            vm.ByKey["Vs"].Value="209";vm.ByKey["PGA"].Value="3";Check(!vm.HasResults&&!vm.ExportCommand.CanExecute(null),"Errore di dominio non gestito");
            Check(listener.Errors.Count==0,string.Join("\n",listener.Errors));
        }
        finally{window.Close();source.Listeners.Remove(listener);source.Switch.Level=previous;}
        Console.WriteLine("PASS muro WPF: tema, geometria, calcolo automatico, grip, JSON/MDST, export, errori e layout");
    }
}
