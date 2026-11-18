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
/// <summary>Riquadro di verifica nella sintesi: Ok null = rapporto senza esito (motore originale).</summary>
public sealed record WallCheckTile(string Label, string Static, string Seismic, bool? StaticOk, bool? SeismicOk);
/// <summary>Motore di calcolo selezionabile: il rivisto è predefinito, l'originale resta per il confronto.</summary>
public enum WallEngine { Rivisto, Originale }
public sealed record WallEngineOption(WallEngine Engine, string Label);

/// <summary>
/// Stato indipendente per ogni progetto. Ogni modifica valida di un campo o di un grip del disegno
/// ricalcola subito geometria e risultati (MVVM: le viste osservano soltanto le proprietà).
/// Un input non valido azzera i risultati e disabilita le esportazioni, senza sostituire valori.
/// </summary>
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
    /// <summary>Risultato del motore originale (sempre calcolato se possibile, per confronto ed esportazione).</summary>
    public WallResult? Result { get; private set; }
    /// <summary>Risultato del motore rivisto NTC 2018.</summary>
    public WallDesignResult? Design { get; private set; }
    public IReadOnlyList<WallEngineOption> Engines { get; } = [new(WallEngine.Rivisto,"Rivisto NTC 2018"),new(WallEngine.Originale,"Originale (confronto)")];
    private WallEngine engine = WallEngine.Rivisto;
    public WallEngine Engine { get => engine; set { if (Set(ref engine,value)) Invalidate(); } }
    public IReadOnlyList<WallResultRow> Results { get; private set; } = [];
    public IReadOnlyList<WallCheckTile> Checks { get; private set; } = [];
    public IReadOnlyList<string> Notes => (Engine==WallEngine.Rivisto ? Design?.Notes : Result?.Notes) ?? [Engine==WallEngine.Rivisto ? WallDesignCalculator.Scope : WallCalculator.Scope,
        "Correggere i dati evidenziati per ottenere risultati e note."];
    public bool HasResults => Engine==WallEngine.Rivisto ? Design is not null : Result is not null;
    /// <summary>Diagramma delle pressioni del motore selezionato.</summary>
    public IReadOnlyList<WallPressurePoint>? Pressures => Engine==WallEngine.Rivisto ? Design?.Pressures : Result?.Pressures;
    public string AreaText => Geometry is null ? "—" : N(WallGeometry.Properties(Geometry.Wall).Area) + " m²";
    public string ResultStatus => !HasResults ? "Risultati non disponibili: correggere i dati evidenziati"
        : Engine==WallEngine.Rivisto ? (Design!.AllSatisfied ? "Verifiche di ribaltamento e scorrimento soddisfatte (Rd/Ed ≥ 1)." : "Almeno una verifica non è soddisfatta (Rd/Ed < 1).")
        : "Motore originale: rapporti di confronto senza esito normativo.";
    /// <summary>Sintesi sempre visibile accanto al disegno: aggiornata insieme alla tabella completa.</summary>
    public IReadOnlyList<WallResultRow> Summary { get; private set; } = [];
    /// <summary>Grip del disegno: posizioni e regole di conversione sono nel core, testabili senza WPF.</summary>
    public IReadOnlyList<WallGrip> Grips => WallEditing.Grips;
    public ParameterCommand<WallGripDrag> GripCommand { get; }
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
        // Il ricalcolo è automatico; il comando resta per forzarlo e per compatibilità con i flussi esistenti.
        CalculateCommand = new(()=>Run(Calculate),()=>InputsValid);
        GripCommand = new(Drag,_=>InputsValid);
        ExampleCommand = new(()=>Load(WallFiles.Example()));
        OpenCommand = new(()=>Run(()=>
        {
            var path=files.Open("Progetto muro di sostegno|*.json;*.mdst");
            if(path is not null) { var doc=WallFiles.Read(files.Read(path)); Load(doc); if(HasResults) Status="Progetto aperto e calcolato."; }
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
            if(path is not null) { files.Write(path,WallFiles.WriteResults(Document(),Design,Result)); Status="Dati e risultati JSON esportati."; }
        }),()=>HasResults);
        Invalidate(); if(HasResults) Status="Dati iniziali del programma originale. Modificare i campi o trascinare i grip del disegno.";
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
        Invalidate(); if(HasResults) Status="Progetto caricato e calcolato.";
    }
    private void Save(bool mdst)
    {
        var doc=Document(); var content=mdst?WallFiles.WriteMdst(doc):WallFiles.Write(doc);
        var path=files.Save(mdst?"Progetto originale MDST|*.mdst":"Progetto muro JSON|*.json",mdst?"muro-sostegno.mdst":"muro-sostegno.json");
        if(path is not null) {files.Write(path,content);Status="Progetto salvato.";}
    }
    private void Calculate()
    {
        Result=null; Design=null; Results=[]; Summary=[]; Checks=[]; NotifyResults();
        var parameters=Document().Parameters;
        // I due motori sono indipendenti: un errore di dominio dell'uno non impedisce i risultati dell'altro.
        string? Try(Action run){ try { run(); return null; } catch(Exception ex) when(ex is ArgumentException or ArithmeticException){ return ex.Message; } }
        var designError=Try(()=>Design=WallDesignCalculator.Calculate(parameters));
        var originalError=Try(()=>Result=WallCalculator.Calculate(parameters));
        var engineError=Engine==WallEngine.Rivisto?designError:originalError;
        if(engineError is not null){NotifyResults();throw new ArgumentException(engineError);}
        if(Engine==WallEngine.Rivisto){Results=DesignRows(Design!);Summary=DesignSummary(Design!);Checks=DesignChecks(Design!);}
        else{Results=Rows(Result!);Summary=SummaryRows(Result!);Checks=OriginalChecks(Result!);}
        NotifyResults(); Status="Calcolo completato.";
    }
    /// <summary>
    /// Reazione a ogni modifica: geometria e calcolo vengono rieseguiti subito.
    /// Il calcolo dura pochi microsecondi, quindi è eseguito in modo sincrono ad ogni carattere digitato.
    /// </summary>
    private void Invalidate()
    {
        if(loading)return;
        Result=null;Design=null;Results=[];Summary=[];Checks=[];Geometry=null;
        try { Geometry=WallGeometry.Create(Document().Parameters);InputError=""; }
        catch(ArgumentException ex) {InputError=ex.Message;}
        catch(System.IO.InvalidDataException ex) {InputError=ex.Message;}
        Changed(nameof(Geometry));Changed(nameof(AreaText));
        CalculateCommand?.Refresh();SaveCommand?.Refresh();SaveMdstCommand?.Refresh();GripCommand?.Refresh();
        if(!InputsValid){NotifyResults();Status=InputError;return;}
        // Errori di dominio del motore (radicandi negativi, divisioni nulle): risultati assenti e messaggio visibile.
        Run(Calculate);
        if(!HasResults)NotifyResults();
        else Status="Risultati aggiornati.";
    }
    private void NotifyResults()
    {
        foreach(var name in new[]{nameof(Result),nameof(Design),nameof(Pressures),nameof(Results),nameof(Summary),nameof(Checks),nameof(HasResults),nameof(Notes),nameof(ResultStatus)})Changed(name);
        ExportCommand?.Refresh();ExportJsonCommand?.Refresh();
    }
    /// <summary>
    /// Applica un trascinamento: i nuovi valori sono scritti nei campi come se fossero digitati,
    /// con un solo ricalcolo finale. Un trascinamento non ammesso lascia invariati campi e risultati.
    /// </summary>
    private void Drag(WallGripDrag drag)
    {
        Dictionary<string,double> current;
        try { current=Document().Parameters; } catch(ArgumentException) { return; }
        var next=WallEditing.Drag(current,drag.Id,drag.Target,drag.Step);
        if(next is null) { Status=$"{drag.Id}: posizione non ammessa dalla geometria, valore invariato."; return; }
        var changed=next.Where(p=>current[p.Key]!=p.Value).ToArray();
        if(changed.Length==0)return;
        loading=true;
        try { foreach(var (key,value) in changed) ByKey[key].Value=value.ToString("G",Italian); }
        finally { loading=false; }
        Invalidate();
        if(HasResults) Status=string.Join(" · ",changed.Select(p=>$"{p.Key} = {ByKey[p.Key].Value} {ByKey[p.Key].Unit}"))+" (dal disegno)";
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
    /// <summary>Grandezze principali per il pannello di sintesi; la tabella completa resta nella scheda Risultati.</summary>
    private static IReadOnlyList<WallResultRow> SummaryRows(WallResult r) => [
        new("KA",N(r.Static.Active,3),N(r.Seismic.Active,3),""),
        new("KP",N(r.Static.Passive,3),N(r.Seismic.Passive,3),""),
        new("M stab. A1",N(r.Stabilizing,1),N(r.Stabilizing,1),"kN·m/m"),
        new("M rib. A1",N(r.OverturningStatic,1),N(r.Overturning,1),"kN·m/m"),
        new("Mstab/Mrib",r.StaticRatio is double a?N(a):"—",r.SeismicRatio is double b?N(b):"—",""),
        new("Spinta scorr. G2","—",N(r.SlidingThrust.G2,1),"kN/m"),
        new("Resist. scorr. G1",N(r.SlidingResistance.G1,1),"—","kN/m"),
        new("Resist. scorr. G2",N(r.SlidingResistance.G2,1),"—","kN/m")
    ];
    private static string R(double? ratio) => ratio is double r ? N(r) : "—";
    private static string Verdict(WallCheck c) => $"{R(c.Ratio)} {(c.Satisfied ? "✓" : "✗")}";
    private static IReadOnlyList<WallCheckTile> DesignChecks(WallDesignResult r) => [
        new("Ribaltamento · Rd/Ed",R(r.StaticOverturning.Overturning.Ratio),R(r.SeismicOverturning.Overturning.Ratio),r.StaticOverturning.Overturning.Satisfied,r.SeismicOverturning.Overturning.Satisfied),
        new("Scorrimento · Rd/Ed",R(r.StaticSliding.Sliding.Ratio),R(r.SeismicSliding.Sliding.Ratio),r.StaticSliding.Sliding.Satisfied,r.SeismicSliding.Sliding.Satisfied)];
    private static IReadOnlyList<WallCheckTile> OriginalChecks(WallResult r) => [
        new("Mstab/Mrib (originale)",R(r.StaticRatio),R(r.SeismicRatio),null,null)];
    /// <summary>Sintesi del motore rivisto: azioni di progetto delle combinazioni determinanti.</summary>
    private static IReadOnlyList<WallResultRow> DesignSummary(WallDesignResult r)
    {
        var so=r.StaticOverturning; var ss=r.StaticSliding; var eo=r.SeismicOverturning; var es=r.SeismicSliding;
        return [
            new("kh / kv","—",$"{N(r.Kh,3)} / ±{N(.5*r.Kh,3)}",""),
            new("KA scorrimento",N(ss.KaSliding,3),N(es.KaSliding,3),""),
            new("M rib. d",N(so.OverturningMoment,1),N(eo.OverturningMoment,1),"kN·m/m"),
            new("M stab. d",N(so.StabilizingMoment,1),N(eo.StabilizingMoment,1),"kN·m/m"),
            new("Ed scorr.",N(ss.Driving,1),N(es.Driving,1),"kN/m"),
            new("Rd scorr.",N(ss.Sliding.Resistance/ss.Sliding.GammaR,1),N(es.Sliding.Resistance/es.Sliding.GammaR,1),"kN/m"),
            new("Sottospinta",N(ss.Uplift,1),N(es.Uplift,1),"kN/m")];
    }
    /// <summary>Tabella completa del motore rivisto: statica con la combinazione propria di ogni verifica, sisma la più gravosa.</summary>
    private static IReadOnlyList<WallResultRow> DesignRows(WallDesignResult r)
    {
        var so=r.StaticOverturning; var ss=r.StaticSliding; var eo=r.SeismicOverturning; var es=r.SeismicSliding;
        return [
            new("Combinazione · ribaltamento",so.Factors,eo.Name,""),
            new("Combinazione · scorrimento",ss.Factors,es.Name,""),
            new("Coefficiente sismico B_H",  "—",N(r.BH,3),"—"),
            new("kh · kv","—",$"{N(r.Kh,4)} · {N(eo.Kv,4)}","—"),
            new("θ = atan[kh/(1 ∓ kv)]","—",N(eo.ThetaDegrees,3),"°"),
            new("KA ribaltamento",N(so.KaOverturning,4),N(eo.KaOverturning,4),"—"),
            new("KA scorrimento",N(ss.KaSliding,4),N(es.KaSliding,4),"—"),
            new("KP a valle (δ = 0)",N(ss.Kp,4),N(es.Kp,4),"—"),
            new("Momento ribaltante di progetto",N(so.OverturningMoment),N(eo.OverturningMoment),"kN·m/m"),
            new("Momento stabilizzante di progetto",N(so.StabilizingMoment),N(eo.StabilizingMoment),"kN·m/m"),
            new("γR ribaltamento",N(so.Overturning.GammaR),N(eo.Overturning.GammaR),"—"),
            new("Ribaltamento · Mstab/(γR·Mrib)",Verdict(so.Overturning),Verdict(eo.Overturning),"—"),
            new("Spinta del terreno (orizzontale)",N(ss.EarthThrust),N(es.EarthThrust),"kN/m"),
            new("Spinta del sovraccarico",N(ss.SurchargeThrust),N(es.SurchargeThrust),"kN/m"),
            new("Spinta dell'acqua a monte",N(ss.WaterBack),N(es.WaterBack),"kN/m"),
            new("Spinta dell'acqua a valle",N(ss.WaterFront),N(es.WaterFront),"kN/m"),
            new("Forze d'inerzia",N(ss.Inertia),N(es.Inertia),"kN/m"),
            new("Sottospinta",N(ss.Uplift),N(es.Uplift),"kN/m"),
            new("Azione orizzontale di progetto Ed",N(ss.Driving),N(es.Driving),"kN/m"),
            new("Azione normale di progetto N",N(ss.Normal),N(es.Normal),"kN/m"),
            new("Resistenza passiva a valle",N(ss.Passive),N(es.Passive),"kN/m"),
            new("Resistenza di progetto Rd",N(ss.Sliding.Resistance/ss.Sliding.GammaR),N(es.Sliding.Resistance/es.Sliding.GammaR),"kN/m"),
            new("Scorrimento · Rd/Ed",Verdict(ss.Sliding),Verdict(es.Sliding),"—"),
            new("Area muro",N(r.Wall.Area),"—","m²"),
            new("Baricentro muro · x; y",$"{N(r.Wall.Centroid.X)}; {N(r.Wall.Centroid.Y)}","—","m")];
    }
    private string Report()
    {
        if(!HasResults)throw new InvalidOperationException("Calcolare prima i risultati.");
        var sb=new StringBuilder($"TA-Calcolo · {Title}\nCalcolato il {DateTime.Now:dd/MM/yyyy HH:mm}\nMotore: {Engines.Single(e=>e.Engine==Engine).Label}\n\nINPUT\n");
        foreach(var p in WallParameters.All)sb.AppendLine($"{p.Label}: {ByKey[p.Key].Value} {p.Unit}");
        sb.AppendLine("\nRISULTATI PER METRO DI SVILUPPO\nGrandezza\tStatica\tSisma\tUnità");
        foreach(var row in Results)sb.AppendLine($"{row.Label}\t{row.Static}\t{row.Seismic}\t{row.Unit}");
        sb.AppendLine("\nNOTE E LIMITI");foreach(var note in Notes)sb.AppendLine("• "+note);
        return sb.ToString();
    }
}
