using System.ComponentModel;
using System.Globalization;
using System.Text;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
using STA.MuroSostegno.Core;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Editor numerico che conserva virgola/punto e segnala gli input incompleti senza sostituirli.</summary>
public sealed class WallNumberField(WallParameter parameter) : ObservableObject, IDataErrorInfo
{
    public string Key => parameter.Key;
    public string Label => parameter.Label;
    public string Unit => parameter.Unit;
    private string value = parameter.Default.ToString("G", CultureInfo.GetCultureInfo("it-IT"));
    public string Value { get => value; set { if (Set(ref this.value,value)) { Changed(nameof(IsValid)); Changed(nameof(Error)); } } }
    public bool TryNumber(out double number) => double.TryParse((Value??"").Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out number) && double.IsFinite(number);
    public bool IsValid => TryNumber(out _);
    public string Error => IsValid ? "" : "Inserire un numero finito, con punto o virgola decimale.";
    public string this[string columnName] => columnName == nameof(Value) ? Error : "";
}
public sealed record WallFieldGroup(string Title, IReadOnlyList<WallNumberField> Fields);
public sealed record WallResultRow(string Label, string Static, string Seismic, string Unit);

/// <summary>Stato indipendente per ogni progetto: invalidazione immediata di risultati ed esportazioni dopo ogni modifica.</summary>
public sealed class RetainingWallViewModel : ObservableObject
{
    private readonly IFileService files;
    private bool loading;
    private string title = "Muro di sostegno", status = "", inputError = "";
    public string Title { get => title; set { if (Set(ref title,value)) Invalidate(); } }
    public string Status { get => status; private set => Set(ref status,value); }
    public string InputError { get => inputError; private set { if (Set(ref inputError,value)) Changed(nameof(InputsValid)); } }
    public bool InputsValid => InputError.Length == 0;
    public IReadOnlyDictionary<string,WallNumberField> ByKey { get; }
    public IReadOnlyList<WallFieldGroup> Groups { get; }
    public WallGeometry? Geometry { get; private set; }
    public WallResult? Result { get; private set; }
    public IReadOnlyList<WallResultRow> Results { get; private set; } = [];
    public IReadOnlyList<string> Notes => Result?.Notes ?? [WallCalculator.Scope,
        "L'esempio riproduce i dati del programma consegnato. Controllare le unità, in particolare Vs, e le ipotesi prima del calcolo."];
    public bool HasResults => Result is not null;
    public string AreaText => Geometry is null ? "—" : N(WallGeometry.Properties(Geometry.Wall).Area) + " m²";
    public string StaticText => Result?.StaticRatio is double r ? N(r) : "—";
    public string SeismicText => Result?.SeismicRatio is double r ? N(r) : "—";
    public string ResultStatus => HasResults ? "Rapporti del motore originale · consultare le note di calcolo" : "Risultati da calcolare";
    public RelayCommand CalculateCommand { get; }
    public RelayCommand ExampleCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveMdstCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ExportJsonCommand { get; }
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private static string N(double value, int digits=2) => value.ToString("F"+digits,Italian);

    public RetainingWallViewModel(IFileService files)
    {
        this.files=files;
        ByKey = WallParameters.All.ToDictionary(p=>p.Key,p=>new WallNumberField(p));
        Groups = WallParameters.All.GroupBy(p=>p.Group).Select(g=>new WallFieldGroup(g.Key,g.Select(p=>ByKey[p.Key]).ToArray())).ToArray();
        foreach (var field in ByKey.Values) field.PropertyChanged += (_,e) => { if(e.PropertyName==nameof(WallNumberField.Value)) Invalidate(); };
        CalculateCommand = new(()=>Run(Calculate),()=>InputsValid);
        ExampleCommand = new(()=>Load(WallFiles.Example()));
        OpenCommand = new(()=>Run(()=>
        {
            var path=files.Open("Progetto muro di sostegno|*.json;*.mdst");
            if(path is not null) { var doc=WallFiles.Read(files.Read(path)); Load(doc); Status="Progetto aperto. Premere Calcola."; }
        }));
        SaveCommand = new(()=>Run(()=>Save(false)),()=>InputsValid);
        SaveMdstCommand = new(()=>Run(()=>Save(true)),()=>InputsValid);
        ExportCommand = new(()=>Run(()=>
        {
            var path=files.Save("Risultati di calcolo|*.txt","muro-sostegno-risultati.txt");
            if(path is not null) { files.Write(path,Report()); Status="Risultati e note esportati."; }
        }),()=>HasResults);
        ExportJsonCommand = new(()=>Run(()=>
        {
            var path=files.Save("Risultati muro JSON|*.json","muro-sostegno-risultati.json");
            if(path is not null) { files.Write(path,WallFiles.WriteResults(Document(),Result!)); Status="Dati e risultati JSON esportati."; }
        }),()=>HasResults);
        Invalidate(); Status="Dati iniziali del programma originale. Modificare i parametri oppure aprire un file .mdst.";
    }
    public WallDocument Document()
    {
        var d=new Dictionary<string,double>();
        foreach(var (key,field) in ByKey)
        {
            if(!field.TryNumber(out var value)) throw new ArgumentException($"{field.Label}: numero non valido.");
            d.Add(key,value);
        }
        var doc=new WallDocument(1,WallFiles.ModuleId,Title,d); WallFiles.Validate(doc); return doc;
    }
    public void Load(WallDocument doc)
    {
        WallFiles.Validate(doc); // Import atomico: nessun campo viene modificato prima della validazione completa.
        loading=true;
        // La rappresentazione più breve conserva il double senza esporre code come 0,59999999999999998.
        try { Title=doc.Title; foreach(var (key,field) in ByKey) field.Value=doc.Parameters[key].ToString("G",Italian); }
        finally { loading=false; }
        Invalidate(); Status="Progetto caricato. Premere Calcola.";
    }
    private void Save(bool mdst)
    {
        var doc=Document(); var content=mdst?WallFiles.WriteMdst(doc):WallFiles.Write(doc);
        var path=files.Save(mdst?"Progetto originale MDST|*.mdst":"Progetto muro JSON|*.json",mdst?"muro-sostegno.mdst":"muro-sostegno.json");
        if(path is not null) {files.Write(path,content);Status="Progetto salvato.";}
    }
    private void Calculate()
    {
        Result=null; Results=[]; NotifyResults();
        Result=WallCalculator.Calculate(Document().Parameters);
        Results=Rows(Result); NotifyResults(); Status="Calcolo completato. Consultare risultati e note del motore originale.";
    }
    private void Invalidate()
    {
        if(loading)return;
        Result=null;Results=[];Geometry=null;
        try { Geometry=WallGeometry.Create(Document().Parameters);InputError=""; }
        catch(ArgumentException ex) {InputError=ex.Message;}
        catch(System.IO.InvalidDataException ex) {InputError=ex.Message;}
        Changed(nameof(Geometry));Changed(nameof(AreaText));NotifyResults();
        CalculateCommand?.Refresh();SaveCommand?.Refresh();SaveMdstCommand?.Refresh();
        Status=InputsValid?"Dati modificati: premere Calcola.":InputError;
    }
    private void NotifyResults()
    {
        foreach(var name in new[]{nameof(Result),nameof(Results),nameof(HasResults),nameof(Notes),nameof(StaticText),nameof(SeismicText),nameof(ResultStatus)})Changed(name);
        ExportCommand?.Refresh();ExportJsonCommand?.Refresh();
    }
    private void Run(Action action)
    {
        try {action();}
        catch(Exception ex) when(ex is ArgumentException or System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException or ArithmeticException)
        {Status="Operazione non completata: "+ex.Message;}
    }
    /// <summary>Righe esplicite: statica e sisma restano separate, lo scorrimento mantiene le componenti originali.</summary>
    private static IReadOnlyList<WallResultRow> Rows(WallResult r) => [
        new("Coefficiente attivo KA",N(r.Static.Active,4),N(r.Seismic.Active,4),"—"),
        new("Coefficiente passivo KP",N(r.Static.Passive,4),N(r.Seismic.Passive,4),"—"),
        new("αH",N(r.Static.AlphaH,4),N(r.Seismic.AlphaH,4),"—"),
        new("θ equivalente",N(r.Static.Theta,4),N(r.Seismic.Theta,4),"°"),
        new("ψ attivo",N(r.Static.PsiActive,4),N(r.Seismic.PsiActive,4),"°"),
        new("ψ passivo",N(r.Static.PsiPassive,4),N(r.Seismic.PsiPassive,4),"°"),
        new("Momento stabilizzante · A1 fav.",N(r.Stabilizing),N(r.Stabilizing),"kN·m/m"),
        new("Momento stabilizzante nominale",N(r.StabilizingNominal),N(r.StabilizingNominal),"kN·m/m"),
        new("Momento ribaltante · A1 sfav.",N(r.OverturningStatic),N(r.Overturning),"kN·m/m"),
        new("Momento ribaltante nominale",N(r.OverturningStaticNominal),N(r.OverturningNominal),"kN·m/m"),
        new("Rapporto Mstab / Mrib",r.StaticRatio is double a?N(a):"Non definito",r.SeismicRatio is double b?N(b):"Non definito","—"),
        new("Spinta scorrimento · G1","—",N(r.SlidingThrust.G1),"kN/m"),
        new("Spinta scorrimento · G2","—",N(r.SlidingThrust.G2),"kN/m"),
        new("Spinta scorrimento · Q","—",N(r.SlidingThrust.Q),"kN/m"),
        new("Resistenza scorrimento · G1",N(r.SlidingResistance.G1),"—","kN/m"),
        new("Resistenza scorrimento · G2",N(r.SlidingResistance.G2),"—","kN/m"),
        new("Resistenza scorrimento · Q",N(r.SlidingResistance.Q),"—","kN/m"),
        new("Area muro",N(r.Wall.Area),"—","m²"),
        new("Baricentro muro · x; y",$"{N(r.Wall.Centroid.X)}; {N(r.Wall.Centroid.Y)}","—","m"),
        new("Area terreno anteriore",N(r.FrontSoil.Area),"—","m²"),
        new("Baricentro terreno anteriore · x; y",$"{N(r.FrontSoil.Centroid.X)}; {N(r.FrontSoil.Centroid.Y)}","—","m"),
        new("Area terreno posteriore",N(r.BackSoil.Area),"—","m²"),
        new("Baricentro terreno posteriore · x; y",$"{N(r.BackSoil.Centroid.X)}; {N(r.BackSoil.Centroid.Y)}","—","m")
    ];
    private string Report()
    {
        if(Result is null)throw new InvalidOperationException("Calcolare prima i risultati.");
        var sb=new StringBuilder($"TA-Calcolo · {Title}\nCalcolato il {DateTime.Now:dd/MM/yyyy HH:mm}\n\nINPUT\n");
        foreach(var p in WallParameters.All)sb.AppendLine($"{p.Label}: {ByKey[p.Key].Value} {p.Unit}");
        sb.AppendLine("\nRISULTATI PER METRO DI SVILUPPO\nGrandezza\tStatica\tSisma\tUnità");
        foreach(var row in Results)sb.AppendLine($"{row.Label}\t{row.Static}\t{row.Seismic}\t{row.Unit}");
        sb.AppendLine("\nNOTE E LIMITI");foreach(var note in Notes)sb.AppendLine("• "+note);
        return sb.ToString();
    }
}
