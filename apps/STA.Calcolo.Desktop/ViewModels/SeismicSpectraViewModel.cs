using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
using STA.Carichi.Sisma;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Riga della tabella degli stati limite: accelerazioni sempre in frazione di g, con il valore in m/s² accanto.</summary>
public sealed record SeismicStateRow(string Name, string Tr, string AgG, string Ag, string F0, string TcStar, string S, string AmaxG, string Amax, string Tb, string Tc, string Td);

/// <summary>
/// Modulo spettri sismici (MVVM): dati del sito e dell'opera, ricalcolo a ogni modifica, curve NTC 2018 in Se/g,
/// tabella degli stati limite e scelta geografica in una finestra dedicata.
/// In modalità di selezione (richiamato da un altro modulo) la conferma restituisce i valori dello stato limite scelto.
/// Il file di progetto resta compatibile con i JSON precedenti (modulo "sisma"): i campi nuovi hanno valori predefiniti.
/// </summary>
public sealed class SeismicSpectraViewModel : ObservableObject
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string N(double v, int d) => v.ToString("N" + d, It);
    private readonly IFileService files;
    private readonly IDialogService dialogs;
    private bool loading;

    public SeismicSpectraViewModel(IFileService files, IDialogService? dialogs = null, bool pickMode = false)
    {
        this.files = files; this.dialogs = dialogs ?? new DialogService(files); PickMode = pickMode;
        ChooseLocationCommand = new(() =>
        {
            var start = TryNumber(longitude, out var x) && TryNumber(latitude, out var y) ? new GeographicPoint(x, y) : new GeographicPoint(12.19944, 44.41778);
            var chosen = this.dialogs.PickLocation(start, Place);
            if (chosen is null) return;
            loading = true;
            try { Longitude = chosen.Point.Longitude.ToString("0.000000", It); Latitude = chosen.Point.Latitude.ToString("0.000000", It); Place = chosen.Name; }
            finally { loading = false; }
            Recalculate(); if (HasResults) Status = "Sito scelto: " + (Place.Length > 0 ? Place : "coordinate dalla mappa") + ".";
        });
        SaveCommand = new(() => Run(() =>
        {
            var path = files.Save("Progetto spettri|*.json", "spettri.json");
            if (path is not null) { files.Write(path, ModuleFiles.Write(Document())); Status = "Progetto salvato."; }
        }), () => HasResults);
        OpenCommand = new(() => Run(() =>
        {
            var path = files.Open("Progetto spettri|*.json");
            if (path is not null) { Load(ModuleFiles.Read<ModuleDocument>(files.Read(path))); Status = "Progetto aperto."; }
        }));
        ExportCommand = new(() => Run(() =>
        {
            var bytes = SeismicSpectra.Zip(Result!, ModuleFiles.Write(Document()));
            var path = files.Save("Spettri TXT (ZIP)|*.zip", "spettri-ntc2018.zip");
            if (path is not null) { files.WriteBytes(path, bytes); Status = "Esportati gli spettri (T, Se/g, Se in m/s²) e il progetto."; }
        }), () => HasResults);
        ConfirmCommand = new(() => CloseRequested?.Invoke(true), () => PickMode && HasResults);
        CancelCommand = new(() => CloseRequested?.Invoke(false));
        Recalculate();
    }

    public bool PickMode { get; }
    public event Action<bool>? CloseRequested;

    // ── Dati ────────────────────────────────────────────────────────────────
    public IReadOnlyList<string> Lives { get; } = ["10", "50", "100"];
    public IReadOnlyList<string> UseClasses { get; } = ["I", "II", "III", "IV"];
    public IReadOnlyList<string> Soils { get; } = Enum.GetNames<TipiTerreni>();
    public IReadOnlyList<string> Topographies { get; } = Enum.GetNames<TopographyCategory>();
    public IReadOnlyList<string> States { get; } = SeismicSpectra.LimitStates;

    private string longitude = "12,199440", latitude = "44,417780", place = "", life = "50", useClass = "II", soil = "A", topography = "T1", q = "1,5", damping = "5", maxPeriod = "4", selectedState = "SLV";
    private bool showHorizontal = true, showVertical, showDesign = true;
    public string Longitude { get => longitude; set { if (Set(ref longitude, value)) InputChanged(); } }
    public string Latitude { get => latitude; set { if (Set(ref latitude, value)) InputChanged(); } }
    /// <summary>Nome del luogo (dalla ricerca per indirizzo o inserito a mano); solo descrittivo.</summary>
    public string Place { get => place; set { if (Set(ref place, value)) Changed(nameof(SiteText)); } }
    public string Life { get => life; set { if (Set(ref life, value)) InputChanged(); } }
    public string UseClass { get => useClass; set { if (Set(ref useClass, value)) InputChanged(); } }
    public string Soil { get => soil; set { if (Set(ref soil, value)) InputChanged(); } }
    public string Topography { get => topography; set { if (Set(ref topography, value)) InputChanged(); } }
    public string Q { get => q; set { if (Set(ref q, value)) InputChanged(); } }
    public string Damping { get => damping; set { if (Set(ref damping, value)) InputChanged(); } }
    public string MaxPeriod { get => maxPeriod; set { if (Set(ref maxPeriod, value)) InputChanged(); } }
    /// <summary>Stato limite evidenziato nel grafico, riassunto e restituito al modulo chiamante.</summary>
    public string SelectedState { get => selectedState; set { if (value is not null && Set(ref selectedState, value)) { NotifyCurves(); NotifySummary(); } } }
    public bool ShowHorizontal { get => showHorizontal; set { if (Set(ref showHorizontal, value)) NotifyCurves(); } }
    public bool ShowVertical { get => showVertical; set { if (Set(ref showVertical, value)) NotifyCurves(); } }
    public bool ShowDesign { get => showDesign; set { if (Set(ref showDesign, value)) NotifyCurves(); } }

    // ── Risultati ───────────────────────────────────────────────────────────
    public SeismicSiteResult? Result { get; private set; }
    public bool HasResults => Result is not null;
    private string inputError = "";
    public string InputError { get => inputError; private set { if (Set(ref inputError, value)) Changed(nameof(InputsValid)); } }
    public bool InputsValid => InputError.Length == 0;
    private string status = "";
    public string Status { get => status; private set => Set(ref status, value); }
    public IReadOnlyList<SeismicStateRow> Rows { get; private set; } = [];
    /// <summary>Curve visibili secondo le opzioni; lo stato limite scelto è disegnato in evidenza dal grafico.</summary>
    public IReadOnlyList<SpectrumCurve> Curves => Result?.Curves.Where(c => c.Design ? ShowDesign : c.Vertical ? ShowVertical : ShowHorizontal).ToArray() ?? [];
    public GeographicPoint? Location => TryNumber(longitude, out var x) && TryNumber(latitude, out var y) ? new(x, y) : null;
    public string SiteText => (Place.Length > 0 ? Place + " · " : "") + (Location is { } p ? $"lat {p.Latitude.ToString("0.00000", It)}° · lon {p.Longitude.ToString("0.00000", It)}°" : "coordinate non valide");
    private SeismicLimitState? Selected => Result?.States.FirstOrDefault(s => s.Name == SelectedState);
    public string AgGText => Selected is { } s ? N(s.AgG, 4) : "—";
    public string AgText => Selected is { } s ? N(s.Ag, 3) + " m/s²" : "—";
    public string AmaxGText => Selected is { } s ? N(s.AmaxG, 4) : "—";
    public string AmaxText => Selected is { } s ? N(s.Amax, 3) + " m/s²" : "—";
    public string SummaryText => Selected is { } s ? $"{s.Name} · TR = {s.Tr} anni · F0 = {N(s.F0, 3)} · Tc* = {N(s.TcStar, 3)} s · S = Ss·ST = {N(s.Ss, 3)} × {N(s.St, 2)} = {N(s.S, 3)}" : InputError;
    /// <summary>Valori restituiti al modulo chiamante (modalità di selezione).</summary>
    public SeismicSiteSelection? Selection => Result is { } r && Selected is { } s ? new(r.Input, s, Place) : null;

    public RelayCommand ChooseLocationCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    // ── Logica ──────────────────────────────────────────────────────────────
    private static bool TryNumber(string text, out double value) =>
        double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private double Number(string text, string label) => TryNumber(text, out var v) ? v : throw new ArgumentException($"{label}: inserire un numero.");

    public SeismicSiteInput Input() => new(Number(longitude, "Longitudine"), Number(latitude, "Latitudine"), (int)Number(life, "Vita nominale"), useClass,
        Enum.Parse<TipiTerreni>(soil), Enum.Parse<TopographyCategory>(topography), Number(q, "Fattore di struttura q"), Number(damping, "Smorzamento ξ"));

    /// <summary>Imposta tutti i dati in un colpo solo (usato dal modulo chiamante), con un unico ricalcolo.</summary>
    public void Apply(SeismicSiteInput input, string placeName)
    {
        loading = true;
        try
        {
            Longitude = input.Longitude.ToString("0.000000", It); Latitude = input.Latitude.ToString("0.000000", It); Place = placeName;
            Life = input.NominalLife.ToString(CultureInfo.InvariantCulture); UseClass = input.UseClass; Soil = input.Soil.ToString(); Topography = input.Topography.ToString();
            Q = input.Q.ToString("G", It); Damping = input.Damping.ToString("G", It);
        }
        finally { loading = false; }
        Recalculate();
    }

    private void InputChanged() { Changed(nameof(SiteText)); if (!loading) Recalculate(); }

    private void Recalculate()
    {
        Result = null; Rows = [];
        try
        {
            Result = SeismicSpectra.Calculate(Input(), Number(maxPeriod, "Periodo massimo"));
            Rows = Result.States.Select(s => new SeismicStateRow(s.Name, s.Tr.ToString(It), N(s.AgG, 4), N(s.Ag, 3), N(s.F0, 3), N(s.TcStar, 3), N(s.S, 3),
                N(s.AmaxG, 4), N(s.Amax, 3), N(s.Tb, 3), N(s.Tc, 3), N(s.Td, 3))).ToArray();
            InputError = ""; Status = "Spettri aggiornati.";
        }
        catch (ArgumentException ex) { InputError = ex.Message; Status = ex.Message; }
        foreach (var name in new[] { nameof(Result), nameof(HasResults), nameof(Rows) }) Changed(name);
        NotifyCurves(); NotifySummary();
        SaveCommand?.Refresh(); ExportCommand?.Refresh(); ConfirmCommand?.Refresh();
    }
    private void NotifyCurves() => Changed(nameof(Curves));
    private void NotifySummary() { foreach (var name in new[] { nameof(AgGText), nameof(AgText), nameof(AmaxGText), nameof(AmaxText), nameof(SummaryText), nameof(Selection) }) Changed(name); }

    /// <summary>Documento di progetto: chiavi del modulo "sisma" storico più topografia, smorzamento, stato e luogo.</summary>
    public ModuleDocument Document()
    {
        var i = Input();
        string Inv(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        return new(1, "sisma", new Dictionary<string, string>
        {
            ["lon"] = Inv(i.Longitude), ["lat"] = Inv(i.Latitude), ["vita"] = i.NominalLife.ToString(CultureInfo.InvariantCulture), ["uso"] = i.UseClass,
            ["terreno"] = i.Soil.ToString(), ["q"] = Inv(i.Q), ["modo"] = "Elastici", ["direzione"] = "Orizzontale", ["periodo"] = Inv(Number(maxPeriod, "Periodo massimo")),
            ["topografia"] = i.Topography.ToString(), ["smorzamento"] = Inv(i.Damping), ["stato"] = SelectedState, ["luogo"] = Place
        }, "");
    }

    /// <summary>Apertura atomica: il file è validato con un calcolo completo prima di modificare i campi.</summary>
    public void Load(ModuleDocument doc)
    {
        if (doc.Module != "sisma") throw new ArgumentException("Il file non è un progetto di spettri sismici.");
        string F(string key, string fallback) => doc.Fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
        double D(string key, string fallback) => TryNumber(F(key, fallback), out var v) ? v : throw new ArgumentException($"Campo {key} non valido.");
        var input = new SeismicSiteInput(D("lon", ""), D("lat", ""), (int)D("vita", "50"), F("uso", "II"),
            Enum.TryParse<TipiTerreni>(F("terreno", "A"), out var t) ? t : throw new ArgumentException("Sottosuolo non valido."),
            Enum.TryParse<TopographyCategory>(F("topografia", "T1"), out var topo) ? topo : throw new ArgumentException("Topografia non valida."),
            D("q", "1.5"), D("smorzamento", "5"));
        SeismicSpectra.Calculate(input, D("periodo", "4"));
        loading = true;
        try { MaxPeriod = D("periodo", "4").ToString("G", It); var state = F("stato", "SLV"); if (States.Contains(state)) SelectedState = state; }
        finally { loading = false; }
        Apply(input, F("luogo", ""));
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException) { Status = "Operazione non completata: " + ex.Message; }
    }
}
