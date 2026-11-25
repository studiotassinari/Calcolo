using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
using Vento.Aerodinamica;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Voce di un elenco a discesa: valore dell'enumerazione e descrizione leggibile.</summary>
public sealed record WindOption<T>(T Value, string Label);

/// <summary>
/// Azioni del vento su edifici, coperture e tettoie (Circolare 21/01/2019 n. 7, § C3.3.8).
/// Il vento del sito riusa i campi e la validazione di <see cref="WindWorkspace"/>; documento, calcolo e
/// relazione passano da <see cref="WindBuildingProject"/> e dal servizio di relazione. Ogni modifica
/// ricalcola subito i risultati: non c'è un comando "Calcola". Lunghezze in metri, angoli in gradi, pressioni in N/m².
/// </summary>
public sealed class WindBuildingViewModel : ObservableObject
{
    public const string ModuleId = WindBuildingProject.ModuleId;
    private const string ProjectFilter = "Progetto vento sulle costruzioni|*.json";
    private readonly IFileService files;
    private readonly IWindBuildingReportService reports;
    private bool loading;

    private TipologiaCostruzione type = TipologiaCostruzione.CoperturaFaldaDoppia;
    private DirezioneVento direction = DirezioneVento.PerpendicolareAlColmo;
    private SchemaFaldeMultiple multiScheme = SchemaFaldeMultiple.FaldeDoppieConColmi;
    private CasoPressioneInterna internalCase = CasoPressioneInterna.NonConsiderata;
    private FacciaEdificio dominantFace = FacciaEdificio.Sopravento;
    private double b = 20, d = 12, h = 8, alpha = 15, rise = 2, blockage;
    private int spans = 3;
    private WindBuildingInput? lastInput;
    private WindBuildingResult? lastResult;

    public WindBuildingViewModel(IFileService files, IWindBuildingReportService? reports = null)
    {
        this.files = files; this.reports = reports ?? new WindBuildingReportService();
        SiteFields = WindBuildingProject.SiteFields.Select(f => new ModuleFieldViewModel(f)).ToArray();
        foreach (var field in SiteFields) field.PropertyChanged += (_, _) => Recalculate();
        OpenCommand = new(() => Run(Open));
        SaveCommand = new(() => Run(Save));
        WordCommand = new(() => Run(Word), () => HasRows);
        Recalculate();
    }

    #region comandi

    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand WordCommand { get; }

    /// <summary>Salva i dati correnti. Dati non validi non vengono salvati: il motivo compare nella barra di stato.</summary>
    private void Save()
    {
        var document = WindBuildingProject.Write(Input());
        WindBuildingProject.Read(document);
        var path = files.Save(ProjectFilter, "vento-costruzione.json");
        if (path == null) return;
        files.Write(path, ModuleFiles.Write(document)); Status = "Progetto salvato.";
    }

    /// <summary>Carica un progetto: il file è validato per intero prima di modificare i campi, così un file errato non altera il lavoro aperto.</summary>
    private void Open()
    {
        var path = files.Open(ProjectFilter);
        if (path == null) return;
        var input = WindBuildingProject.Read(ModuleFiles.Read<ModuleDocument>(files.Read(path)));
        loading = true;
        try
        {
            foreach (var field in SiteFields) field.Value = input.Site[field.Key];
            var g = input.Geometry;
            type = g.Tipologia; direction = g.Direzione; multiScheme = g.SchemaMultiplo; internalCase = input.InternalCase; dominantFace = input.DominantFace;
            b = g.B; d = g.D; h = g.H; alpha = g.Alfa; rise = g.Freccia; spans = g.NumeroTratti; blockage = g.GradoBloccaggio;
        }
        finally { loading = false; }
        foreach (var name in new[] { nameof(Type), nameof(Direction), nameof(MultiScheme), nameof(InternalCase), nameof(DominantFace),
            nameof(B), nameof(D), nameof(H), nameof(Alpha), nameof(Rise), nameof(Spans), nameof(Blockage) }) Changed(name);
        NotifyLayout(); Recalculate();
        if (HasRows) Status = "Progetto caricato.";
    }

    /// <summary>Esporta il capitolo Word dai risultati mostrati in finestra.</summary>
    private void Word()
    {
        if (lastInput == null || lastResult == null) return;
        var path = files.Save("Capitolo Word|*.docx", "vento-costruzione-capitolo.docx");
        if (path == null) return;
        files.WriteBytes(path, reports.Word(lastInput, lastResult)); Status = "Capitolo Word creato.";
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or System.Text.Json.JsonException or System.IO.IOException or UnauthorizedAccessException)
        { Status = ex.Message; }
    }

    #endregion

    #region elenchi per le caselle a discesa

    private static IReadOnlyList<WindOption<T>> Options<T>(Func<T, string> label) where T : struct, Enum => Enum.GetValues<T>().Select(v => new WindOption<T>(v, label(v))).ToArray();
    public IReadOnlyList<WindOption<TipologiaCostruzione>> Types { get; } = Options<TipologiaCostruzione>(Descrizioni.Di);
    public IReadOnlyList<WindOption<DirezioneVento>> Directions { get; } = Options<DirezioneVento>(Descrizioni.Di);
    public IReadOnlyList<WindOption<SchemaFaldeMultiple>> MultiSchemes { get; } = Options<SchemaFaldeMultiple>(Descrizioni.Di);
    public IReadOnlyList<WindOption<CasoPressioneInterna>> InternalCases { get; } = Options<CasoPressioneInterna>(Descrizioni.Di);
    public IReadOnlyList<WindOption<FacciaEdificio>> Faces { get; } = Options<FacciaEdificio>(Descrizioni.Di);

    #endregion

    #region dati in ingresso

    /// <summary>Campi del vento del sito, con etichette e scelte definite da WindWorkspace.</summary>
    public IReadOnlyList<ModuleFieldViewModel> SiteFields { get; }

    public TipologiaCostruzione Type { get => type; set { if (Set(ref type, value)) { NotifyLayout(); Recalculate(); } } }
    public DirezioneVento Direction { get => direction; set { if (Set(ref direction, value)) Recalculate(); } }
    public SchemaFaldeMultiple MultiScheme { get => multiScheme; set { if (Set(ref multiScheme, value)) Recalculate(); } }
    public CasoPressioneInterna InternalCase { get => internalCase; set { if (Set(ref internalCase, value)) { NotifyLayout(); Recalculate(); } } }
    public FacciaEdificio DominantFace { get => dominantFace; set { if (Set(ref dominantFace, value)) Recalculate(); } }
    /// <summary>Dimensione in pianta ortogonale al vento [m].</summary>
    public double B { get => b; set { if (Set(ref b, value)) Recalculate(); } }
    /// <summary>Dimensione in pianta parallela al vento [m].</summary>
    public double D { get => d; set { if (Set(ref d, value)) Recalculate(); } }
    /// <summary>Quota massima della copertura [m]; per la volta è la quota di imposta.</summary>
    public double H { get => h; set { if (Set(ref h, value)) Recalculate(); } }
    /// <summary>Inclinazione delle falde [°], con il segno delle figure della Circolare.</summary>
    public double Alpha { get => alpha; set { if (Set(ref alpha, value)) Recalculate(); } }
    /// <summary>Freccia della volta [m].</summary>
    public double Rise { get => rise; set { if (Set(ref rise, value)) Recalculate(); } }
    /// <summary>Numero di tratti (campi) delle falde multiple.</summary>
    public int Spans { get => spans; set { if (Set(ref spans, value)) Recalculate(); } }
    /// <summary>Grado di bloccaggio φ delle tettoie, da 0 (libera) a 1 (ostruita).</summary>
    public double Blockage { get => blockage; set { if (Set(ref blockage, value)) Recalculate(); } }

    #endregion

    #region campi visibili in funzione della tipologia

    public bool IsCanopy => type is TipologiaCostruzione.TettoiaFaldaSingola or TipologiaCostruzione.TettoiaFaldaDoppia or TipologiaCostruzione.TettoiaFaldeMultiple;
    public bool ShowAlpha => Descrizioni.HaInclinazione(type);
    public bool ShowRise => type == TipologiaCostruzione.CoperturaVoltaCilindrica;
    public bool ShowSpans => Descrizioni.HaTratti(type);
    public bool ShowMultiScheme => type == TipologiaCostruzione.CoperturaFaldeMultiple;
    public bool ShowDirection => Descrizioni.HaDirezione(type);
    public bool ShowBlockage => IsCanopy;
    public bool ShowInternal => !IsCanopy;
    public bool ShowDominantFace => ShowInternal && internalCase is (CasoPressioneInterna.AperturaDominanteDoppia or CasoPressioneInterna.AperturaDominanteTripla);
    public bool ShowForce => IsCanopy;
    public string HeightLabel => ShowRise ? "h · quota di imposta della volta [m]" : "h · altezza massima della copertura [m]";
    public string AlphaLabel => type switch
    {
        TipologiaCostruzione.CoperturaFaldaSingola => "α · inclinazione [°] (+ lato basso sopravento)",
        TipologiaCostruzione.CoperturaFaldaDoppia or TipologiaCostruzione.TettoiaFaldaDoppia or TipologiaCostruzione.TettoiaFaldeMultiple => "α · inclinazione [°] (+ colmo, − impluvio)",
        _ => "α · inclinazione delle falde [°]"
    };

    #endregion

    #region risultati

    public IReadOnlyList<AzioneSuperficie> Rows { get; private set; } = [];
    public bool HasRows => Rows.Count > 0;
    /// <summary>Valutazioni intermedie: rapporti h/d e f/d, quote di riferimento, campi applicati, letture e interpolazioni.</summary>
    public IReadOnlyList<Valutazione> Evaluations { get; private set; } = [];
    /// <summary>Avvertenze sui limiti di applicabilità delle tabelle, una per riga.</summary>
    public string Warnings { get; private set; } = "";
    public bool HasWarnings => Warnings.Length > 0;
    /// <summary>Riepilogo del vento del sito: velocità, pressione di riferimento e categoria usate.</summary>
    public string SiteSummary { get; private set; } = "";
    public string PeakSummary { get; private set; } = "";
    /// <summary>Valori di cpi adottati e quota zi; vuoto se la pressione interna non è considerata.</summary>
    public string InternalSummary { get; private set; } = "";
    public bool HasInternal => InternalSummary.Length > 0;
    public string NetTitle1 { get; private set; } = "";
    public string NetTitle2 { get; private set; } = "";
    public bool ShowNet1 => NetTitle1.Length > 0;
    public bool ShowNet2 => NetTitle2.Length > 0;
    private string status = "";
    public string Status { get => status; private set => Set(ref status, value); }

    /// <summary>Condizioni di carico alternative (valori negativi / positivi); vuoto se ogni superficie ha un solo valore.</summary>
    public IReadOnlyList<string> Conditions { get; private set; } = [];
    public bool ShowConditions => Conditions.Count > 1;
    private string selectedCondition = "";
    /// <summary>Condizione rappresentata nella figura dei risultati.</summary>
    public string SelectedCondition
    {
        get => selectedCondition;
        set { if (value != null && Set(ref selectedCondition, value)) { Changed(nameof(FigureRows)); Changed(nameof(FigureCaption)); } }
    }
    /// <summary>Righe disegnate nella figura: quelle a valore unico più quelle della condizione scelta.</summary>
    public IReadOnlyList<AzioneSuperficie> FigureRows => lastResult == null || !HasRows ? [] : lastResult.Actions.PerCondizione(selectedCondition);
    public string FigureCaption => selectedCondition.Length > 0 ? "Condizione: " + selectedCondition : "";

    #endregion

    /// <summary>Geometria corrente nella forma richiesta dal motore di calcolo.</summary>
    public GeometriaCostruzione Geometry() => new()
    {
        Tipologia = type, Direzione = ShowDirection ? direction : DirezioneVento.PerpendicolareAlColmo,
        B = b, D = d, H = h, Alfa = alpha, Freccia = rise, NumeroTratti = spans, SchemaMultiplo = multiScheme, GradoBloccaggio = blockage
    };

    /// <summary>Dati completi correnti, come vengono salvati nel progetto.</summary>
    public WindBuildingInput Input() => new(SiteFields.ToDictionary(f => f.Key, f => f.Value), Geometry(), internalCase, dominantFace);

    /// <summary>
    /// Ricalcola tutto. Dati incompleti o fuori campo non producono risultati parziali:
    /// la tabella si svuota e la barra di stato riporta il motivo.
    /// </summary>
    private void Recalculate()
    {
        if (loading) return;
        // La finestra è in italiano (Language="it-IT"): anche i riepiloghi usano sempre la virgola decimale.
        var culture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var input = Input(); var outcome = WindBuildingProject.Calculate(input);
            var state = outcome.Site; var result = outcome.Actions;
            lastInput = input; lastResult = outcome;
            Rows = result.Azioni; Evaluations = result.Valutazioni;
            Warnings = string.Join(Environment.NewLine, result.Avvertenze);
            SiteSummary = string.Create(culture, $"vb = {state.Velocity:0.00} m/s · qr = {state.Qr:0.0} N/m² · categoria {state.Category}");
            PeakSummary = result.Valido ? string.Create(culture, $"qp = {result.PressionePiccoSommità:N0} N/m² alla sommità (z = {input.Geometry.AltezzaSommità:0.##} m)") : "";
            NetTitle1 = result.Cpi.Length > 0 ? "p − pi (" + CalcolatoreAzioni.ConSegno(result.Cpi[0]) + ")" : "";
            NetTitle2 = result.Cpi.Length > 1 ? "p − pi (" + CalcolatoreAzioni.ConSegno(result.Cpi[1]) + ")" : "";
            InternalSummary = result.Cpi.Length == 0 ? "" : string.Create(culture,
                $"cpi = {string.Join(" e ", result.Cpi.Select(CalcolatoreAzioni.ConSegno))} alla quota zi = {result.QuotaInterna:0.##} m · colonne p − pi: pressione netta per ciascun cpi");
            Conditions = result.Condizioni;
            // La condizione scelta resta, se esiste ancora; altrimenti si passa alla prima disponibile.
            selectedCondition = Conditions.Contains(selectedCondition) ? selectedCondition : Conditions.FirstOrDefault() ?? "";
            Status = result.Valido ? "Risultati aggiornati. p = qr · ce(ze) · c · cd, positiva verso la superficie." : result.Avvertenze.FirstOrDefault() ?? "Dati non validi.";
            if (!result.Valido) { lastInput = null; lastResult = null; }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            lastInput = null; lastResult = null;
            Rows = []; Evaluations = []; Warnings = ""; SiteSummary = ""; PeakSummary = ""; InternalSummary = ""; NetTitle1 = ""; NetTitle2 = ""; Conditions = []; selectedCondition = "";
            Status = ex.Message;
        }
        foreach (var name in new[] { nameof(Rows), nameof(HasRows), nameof(Evaluations), nameof(Warnings), nameof(HasWarnings), nameof(SiteSummary), nameof(PeakSummary),
            nameof(InternalSummary), nameof(HasInternal), nameof(NetTitle1), nameof(NetTitle2), nameof(ShowNet1), nameof(ShowNet2),
            nameof(Conditions), nameof(ShowConditions), nameof(SelectedCondition), nameof(FigureRows), nameof(FigureCaption) }) Changed(name);
        WordCommand?.Refresh();
    }

    private void NotifyLayout()
    {
        foreach (var name in new[] { nameof(IsCanopy), nameof(ShowAlpha), nameof(ShowRise), nameof(ShowSpans), nameof(ShowMultiScheme), nameof(ShowDirection),
            nameof(ShowBlockage), nameof(ShowInternal), nameof(ShowDominantFace), nameof(ShowForce), nameof(HeightLabel), nameof(AlphaLabel) }) Changed(name);
    }
}
