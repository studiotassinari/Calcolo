using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Controls;
using STA.Calcolo.Desktop.Services;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Views;

/// <summary>Spettri MVVM: finestra, cursore del grafico, scelta geografica con ricerca simulata e restituzione dei valori al muro.</summary>
internal static class SeismicSpectraSmoke
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private sealed class Errors : TraceListener { public List<string> Items{get;}=[]; public override void Write(string? m){if(m!=null)Items.Add(m);} public override void WriteLine(string? m){if(m!=null)Items.Add(m);} }
    private sealed class OfflineTiles : IMapTileService { public Task<byte[]> ReadAsync(int z,int x,int y,CancellationToken t)=>Task.FromException<byte[]>(new System.IO.IOException("offline")); }
    private sealed class FakeGeocoding : IGeocodingService
    {
        public Task<IReadOnlyList<GeographicPlace>> SearchAsync(string q,CancellationToken t)=>Task.FromResult<IReadOnlyList<GeographicPlace>>([new("Piazza del Popolo, Ravenna",new(12.1994,44.4178)),new("Ravenna (comune)",new(12.2,44.42))]);
    }
    /// <summary>Finestre di dialogo simulate: restituiscono subito un luogo o una selezione.</summary>
    private sealed class FakeDialogs : IDialogService
    {
        public GeographicPlace? PickLocation(GeographicPoint start,string place)=>new("Faenza",new(11.8833,44.2858));
        public SeismicSiteSelection? PickSeismic(SeismicSiteInput? start,string place,string state)
        {
            var vm=new SeismicSpectraViewModel(new MemoryFileService(),this,pickMode:true);vm.Soil="C";vm.SelectedState=state;return vm.Selection;
        }
    }
    public static void Run(Action<Window,string,double> render)
    {
        var listener=new Errors();var source=PresentationTraceSources.DataBindingSource;var previous=source.Switch.Level;source.Switch.Level=SourceLevels.Error;source.Listeners.Add(listener);
        try
        {
            var files=new MemoryFileService();var dialogs=new FakeDialogs();
            var vm=new SeismicSpectraViewModel(files,dialogs);Check(vm.HasResults&&vm.Rows.Count==4&&vm.Curves.Count>=5,"Spettri iniziali mancanti: "+vm.Status);
            var window=new SeismicSpectraWindow{DataContext=vm};render(window,"spettri",1400);
            var chart=(SpectrumPlotView)window.FindName("SpectrumChart");chart.SetCursor(.5);render(window,"spettri-cursore",1400);
            Check(chart.CursorPeriod==.5,"Cursore del grafico non posizionato");
            Check(((DataGrid)window.FindName("StatesGrid")).Columns.Any(c=>(string)c.Header=="ag/g")&&((DataGrid)window.FindName("StatesGrid")).Columns.Any(c=>(string)c.Header=="ag [m/s²]"),"Colonne ag/g e m/s² mancanti");
            // Ricalcolo automatico a ogni modifica; valori in g con il corrispondente in m/s².
            var before=vm.AgGText;vm.Latitude="40,85";vm.Longitude="14,25";Check(vm.HasResults&&vm.AgGText!=before&&vm.AgText.EndsWith("m/s²"),"Ricalcolo non eseguito");
            vm.Q="";Check(!vm.HasResults&&!vm.ExportCommand.CanExecute(null),"Input vuoto accettato");vm.Q="1,5";
            vm.ChooseLocationCommand.Execute(null);Check(vm.Place=="Faenza"&&vm.Longitude.StartsWith("11,88")&&vm.HasResults,"Scelta geografica non applicata");
            files.NextSave="spettri.json";vm.SaveCommand.Execute(null);files.NextOpen="spettri.json";var other=new SeismicSpectraViewModel(files,dialogs);other.OpenCommand.Execute(null);
            Check(other.Place=="Faenza"&&other.AgGText==vm.AgGText,"Progetto non riaperto");
            files.NextSave="spettri.zip";vm.ExportCommand.Execute(null);Check(files.Binary["spettri.zip"].Length>1000,"ZIP mancante");
            window.Close();
            // Finestra di scelta geografica con ricerca simulata.
            var picker=new GeoPickerViewModel(new FakeGeocoding(),new(12.2,44.4),"");var pickerWindow=new GeoPickerWindow{DataContext=picker};
            ((GeographicMapView)pickerWindow.FindName("PickerMap")).TileService=new OfflineTiles();
            picker.Query="Piazza del Popolo Ravenna";picker.SearchCommand.ExecuteAsync().GetAwaiter().GetResult();
            Check(picker.Results.Count==2&&picker.Place.StartsWith("Piazza")&&Math.Abs(picker.Location.Latitude-44.4178)<1e-9,"Ricerca non applicata");
            render(pickerWindow,"scelta-geografica",1100);bool? closed=null;picker.CloseRequested+=ok=>closed=ok;picker.ConfirmCommand.Execute(null);
            Check(closed==true&&picker.Result?.Point.Longitude==12.1994,"Coordinate non restituite");
            picker.Latitude="60";Check(!picker.ConfirmCommand.CanExecute(null),"Sito fuori reticolo accettato");pickerWindow.Close();
            // Modalità di selezione e collegamento col muro: PGA = amax/g dello stato limite scelto.
            var pick=new SeismicSpectraViewModel(files,dialogs,pickMode:true);var pickWindow=new SeismicSpectraWindow{DataContext=pick};render(pickWindow,"spettri-selezione",1400);
            Check(((Button)pickWindow.FindName("ConfirmButton")).Visibility==Visibility.Visible&&pick.ConfirmCommand.CanExecute(null),"Conferma non disponibile");pickWindow.Close();
            var wall=new RetainingWallViewModel(files,dialogService:dialogs);wall.SeismicFromSpectraCommand.Execute(null);
            Check(wall.SeismicSource is {State.Name:"SLV"} s&&Math.Abs(double.Parse(wall.ByKey["PGA"].Value.Replace(',','.'),System.Globalization.CultureInfo.InvariantCulture)-Math.Round(s.State.AmaxG,4))<1e-12&&wall.HasResults,"PGA non importata nel muro");
            Check(listener.Items.Count==0,string.Join("\n",listener.Items));
        }
        finally{source.Listeners.Remove(listener);source.Switch.Level=previous;}
        Console.WriteLine("PASS spettri WPF: ricalcolo, cursore, ag/g e m/s², scelta geografica, ricerca, selezione e PGA nel muro");
    }
}
