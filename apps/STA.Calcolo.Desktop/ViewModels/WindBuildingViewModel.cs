using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
using Vento.Aerodinamica;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Voce di un elenco a discesa: valore dell'enumerazione e descrizione leggibile.</summary>
public sealed record WindOption<T>(T Value, string Label);

/// <summary>
/// Azioni del vento su edifici, coperture e tettoie (Circolare 21/01/2019 n. 7, § C3.3.8).
/// Il vento del sito riusa i campi e la validazione di <see cref="WindWorkspace"/>; coefficienti e
/// pressioni sono calcolati da <see cref="CalcolatoreAzioni"/>. Ogni modifica ricalcola subito i
/// risultati: non c'è un comando "Calcola". Lunghezze in metri, angoli in gradi, pressioni in N/m².
/// </summary>
public sealed class WindBuildingViewModel : ObservableObject
{
    public const string ModuleId = "vento-costruzione";
    // Campi di WindWorkspace mostrati nella vista: altezza e cp sono sostituiti dalla geometria e dai coefficienti calcolati.
    private static readonly string[] SiteKeys = ["modello", "zona", "rugosita", "distanza", "categoria", "quota", "ct", "cd"];
    private readonly WindWorkspace site = new();

    private TipologiaCostruzione type = TipologiaCostruzione.CoperturaFaldaDoppia;
    private DirezioneVento direction = DirezioneVento.PerpendicolareAlColmo;
    private SchemaFaldeMultiple multiScheme = SchemaFaldeMultiple.FaldeDoppieConColmi;
    private CasoPressioneInterna internalCase = CasoPressioneInterna.NonConsiderata;
    private FacciaEdificio dominantFace = FacciaEdificio.Sopravento;
    private double b = 20, d = 12, h = 8, alpha = 15, rise = 2, blockage;
    private int spans = 3;

    public WindBuildingViewModel()
    {
        SiteFields = site.Fields.Where(f => SiteKeys.Contains(f.Key)).Select(f => new ModuleFieldViewModel(f)).ToArray();
        foreach (var field in SiteFields) field.PropertyChanged += (_, _) => Recalculate();
        Recalculate();
    }

    #region elenchi per le caselle a discesa

    public IReadOnlyList<WindOption<TipologiaCostruzione>> Types { get; } = [
        new(TipologiaCostruzione.CoperturaPiana, "Edificio con copertura piana"),
        new(TipologiaCostruzione.CoperturaFaldaSingola, "Edificio con copertura a falda singola"),
        new(TipologiaCostruzione.CoperturaFaldaDoppia, "Edificio con copertura a falda doppia"),
        new(TipologiaCostruzione.CoperturaPadiglione, "Edificio con copertura a padiglione"),
        new(TipologiaCostruzione.CoperturaFaldeMultiple, "Edificio con copertura a falde multiple"),
        new(TipologiaCostruzione.CoperturaVoltaCilindrica, "Edificio con copertura a volta cilindrica"),
        new(TipologiaCostruzione.TettoiaFaldaSingola, "Tettoia a falda singola"),
        new(TipologiaCostruzione.TettoiaFaldaDoppia, "Tettoia a falda doppia"),
        new(TipologiaCostruzione.TettoiaFaldeMultiple, "Tettoia a falde multiple")];

    public IReadOnlyList<WindOption<DirezioneVento>> Directions { get; } = [
        new(DirezioneVento.PerpendicolareAlColmo, "Perpendicolare al colmo / alle generatrici"),
        new(DirezioneVento.ParalleloAlColmo, "Parallelo al colmo / alle generatrici")];

    public IReadOnlyList<WindOption<SchemaFaldeMultiple>> MultiSchemes { get; } = [
        new(SchemaFaldeMultiple.ShedInclinazioneNegativa, "(a) shed, lato alto sopravento"),
        new(SchemaFaldeMultiple.ShedInclinazionePositiva, "(b) shed, lato basso sopravento"),
        new(SchemaFaldeMultiple.FaldeDoppieConColmi, "(c) falde doppie con colmi"),
        new(SchemaFaldeMultiple.FaldeDoppieConImpluvi, "(d) falde doppie con impluvi")];

    public IReadOnlyList<WindOption<CasoPressioneInterna>> InternalCases { get; } = [
        new(CasoPressioneInterna.NonConsiderata, "Non considerata"),
        new(CasoPressioneInterna.PorositàUniforme, "Porosità uniforme: cpi = +0,2 / −0,3"),
        new(CasoPressioneInterna.AperturaDominanteDoppia, "Apertura dominante ≥ 2 volte le altre: cpi = 0,75 cpe"),
        new(CasoPressioneInterna.AperturaDominanteTripla, "Apertura dominante ≥ 3 volte le altre: cpi = 0,90 cpe")];

    public IReadOnlyList<WindOption<FacciaEdificio>> Faces { get; } = [
        new(FacciaEdificio.Sopravento, "Apertura sulla faccia sopravento"),
        new(FacciaEdificio.Laterale, "Apertura su una faccia laterale"),
        new(FacciaEdificio.Sottovento, "Apertura sulla faccia sottovento")];

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
    public bool ShowAlpha => type is not (TipologiaCostruzione.CoperturaPiana or TipologiaCostruzione.CoperturaVoltaCilindrica);
    public bool ShowRise => type == TipologiaCostruzione.CoperturaVoltaCilindrica;
    public bool ShowSpans => type is TipologiaCostruzione.CoperturaFaldeMultiple or TipologiaCostruzione.TettoiaFaldeMultiple;
    public bool ShowMultiScheme => type == TipologiaCostruzione.CoperturaFaldeMultiple;
    public bool ShowDirection => type is not (TipologiaCostruzione.CoperturaPiana or TipologiaCostruzione.CoperturaPadiglione);
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

    #endregion

    /// <summary>Geometria corrente nella forma richiesta dal motore di calcolo.</summary>
    public GeometriaCostruzione Geometry() => new()
    {
        Tipologia = type, Direzione = ShowDirection ? direction : DirezioneVento.PerpendicolareAlColmo,
        B = b, D = d, H = h, Alfa = alpha, Freccia = rise, NumeroTratti = spans, SchemaMultiplo = multiScheme, GradoBloccaggio = blockage
    };

    /// <summary>Documento per WindWorkspace: i campi non mostrati assumono valori neutri (cp = 1) e l'altezza della costruzione.</summary>
    private ModuleDocument SiteDocument(double top)
    {
        var fields = site.Fields.ToDictionary(f => f.Key, f => f.Default);
        foreach (var field in SiteFields) fields[field.Key] = field.Value;
        fields["altezza"] = Math.Clamp(double.IsFinite(top) ? top : 1, .01, 200).ToString(CultureInfo.InvariantCulture);
        fields["cp"] = "1";
        return new(1, site.Id, fields, "");
    }

    /// <summary>
    /// Ricalcola tutto. Dati incompleti o fuori campo non producono risultati parziali:
    /// la tabella si svuota e la barra di stato riporta il motivo.
    /// </summary>
    private void Recalculate()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            var geometry = Geometry();
            var state = site.Read(SiteDocument(geometry.AltezzaSommità));
            var result = CalcolatoreAzioni.Calcola(new(state.Qr, state.Category, state.Ct, state.Cd), geometry, internalCase, dominantFace);
            if (result.Azioni.Any(r => !double.IsFinite(r.Pressione))) throw new ArgumentException("Risultati non finiti: controllare la geometria.");
            Rows = result.Azioni;
            Warnings = string.Join(Environment.NewLine, result.Avvertenze);
            SiteSummary = string.Create(culture, $"vb = {state.Velocity:0.00} m/s · qr = {state.Qr:0.0} N/m² · categoria {state.Category}");
            PeakSummary = result.Valido ? string.Create(culture, $"qp = {result.PressionePiccoSommità:N0} N/m² alla sommità (z = {geometry.AltezzaSommità:0.##} m)") : "";
            NetTitle1 = result.Cpi.Length > 0 ? "p − pi (" + CalcolatoreAzioni.ConSegno(result.Cpi[0]) + ")" : "";
            NetTitle2 = result.Cpi.Length > 1 ? "p − pi (" + CalcolatoreAzioni.ConSegno(result.Cpi[1]) + ")" : "";
            InternalSummary = result.Cpi.Length == 0 ? "" : string.Create(culture,
                $"cpi = {string.Join(" e ", result.Cpi.Select(CalcolatoreAzioni.ConSegno))} alla quota zi = {result.QuotaInterna:0.##} m · colonne p − pi: pressione netta per ciascun cpi");
            Status = result.Valido ? "Risultati aggiornati. p = qr · ce(ze) · c · cd, positiva verso la superficie." : result.Avvertenze.FirstOrDefault() ?? "Dati non validi.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            Rows = []; Warnings = ""; SiteSummary = ""; PeakSummary = ""; InternalSummary = ""; NetTitle1 = ""; NetTitle2 = "";
            Status = ex.Message;
        }
        foreach (var name in new[] { nameof(Rows), nameof(HasRows), nameof(Warnings), nameof(HasWarnings), nameof(SiteSummary), nameof(PeakSummary),
            nameof(InternalSummary), nameof(HasInternal), nameof(NetTitle1), nameof(NetTitle2), nameof(ShowNet1), nameof(ShowNet2) }) Changed(name);
    }

    private void NotifyLayout()
    {
        foreach (var name in new[] { nameof(IsCanopy), nameof(ShowAlpha), nameof(ShowRise), nameof(ShowSpans), nameof(ShowMultiScheme), nameof(ShowDirection),
            nameof(ShowBlockage), nameof(ShowInternal), nameof(ShowDominantFace), nameof(ShowForce), nameof(HeightLabel), nameof(AlphaLabel) }) Changed(name);
    }
}
