using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Orchestra input, figure ed esportazioni senza dipendenze WPF o accessi diretti a file e rete.</summary>
public sealed class ScientificViewModel : ObservableObject
{
    private readonly ICalculationWorkspace module;
    private readonly IFileService files;
    private bool loading;
    public string Title => module.Title;
    public string Description => module.Description;
    public bool IsSnow => module.Id == "neve";
    public bool IsWind => module.Id == "vento";
    public bool IsSeismic => module.Id == "sisma";
    public bool IsConcrete => module.Id == "calcestruzzo";
    public bool HasChart => !IsConcrete;
    public bool HasResults => Results.Count > 0;
    public IReadOnlyList<ModuleFieldViewModel> Fields { get; }
    public IReadOnlyDictionary<string, ModuleFieldViewModel> ByKey { get; }
    public IReadOnlyList<ResultRow> Results { get; private set; } = [];
    public IReadOnlyList<PlotSeries> Series { get; private set; } = [];
    public IReadOnlyList<PlotSeries> ExposureSeries { get; private set; } = [];
    public string XAxis => IsSnow ? "Quota sito [m s.l.m.]" : IsWind ? "Pressione p(z) [N/m²]" : "Periodo T [s]";
    public string YAxis => IsSnow ? "qsk [kN/m²]" : IsWind ? "Altezza z [m]" : "Accelerazione Sa [m/s²]";
    private string status = "Inserire i dati e premere Calcola.";
    public string Status { get => status; private set => Set(ref status, value); }
    public string Context { get; private set; } = "";
    public double RoofAngle { get; private set; }
    public double SnowLoad => HasResults && IsSnow ? Results[3].Valore : double.NaN;
    public string Summary => !HasResults ? "Risultati da calcolare" : IsSnow ? $"qs = {SnowLoad:0.###} kN/m²"
        : IsWind ? $"p(H) = {Results[3].Valore:0.##} N/m²" : IsSeismic ? "Spettri calcolati · q = " + ByKey["q"].Value : "Proprietà del materiale";
    private GeographicPoint location = new(12.19944, 44.41778);
    /// <summary>Il marker trasferisce la coppia atomica lon/lat; il testo manuale non viene arrotondato.</summary>
    public GeographicPoint Location
    {
        get => location;
        set
        {
            if (!IsSeismic || value == null || !double.IsFinite(value.Longitude) || !double.IsFinite(value.Latitude)) return;
            if (!Set(ref location, value)) return;
            loading = true;
            try { ByKey["lon"].Value = value.Longitude.ToString("0.000000", CultureInfo.InvariantCulture); ByKey["lat"].Value = value.Latitude.ToString("0.000000", CultureInfo.InvariantCulture); }
            finally { loading = false; }
            InputsChanged();
        }
    }
    public RelayCommand CalculateCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ExportSpectraCommand { get; }
    public ScientificViewModel(string id, IFileService files)
    {
        module = WorkspaceCatalog.Create().Single(m => m.Id == id); this.files = files;
        Fields = module.Fields.Select(f => new ModuleFieldViewModel(f)).ToArray();
        ByKey = Fields.ToDictionary(f => f.Key);
        foreach (var field in Fields) field.PropertyChanged += (_, _) => { if (!loading) InputsChanged(); };
        CalculateCommand = new(() => Run(Calculate));
        SaveCommand = new(() => Run(() => {
            var doc = Document(); module.Calculate(doc);
            var path = files.Save("Progetto modulo|*.json", id + ".json");
            if (path != null) { files.Write(path, ModuleFiles.Write(doc)); Status = "Progetto salvato."; }
        }));
        OpenCommand = new(() => Run(() => { var path = files.Open("Progetto modulo|*.json"); if (path != null) Load(ModuleFiles.Read<ModuleDocument>(files.Read(path))); }));
        ExportCommand = new(() => Run(() => {
            var path = files.Save("Risultati JSON|*.json", id + "-risultati.json");
            if (path != null) files.Write(path, ModuleFiles.Write(new { Input = Document(), Results }));
        }), () => HasResults);
        ExportSpectraCommand = new(() => Run(() => {
            var bytes = SeismicTextExport.Zip(Document());
            var path = files.Save("Tutti gli spettri TXT (ZIP)|*.zip", "spettri-testo.zip");
            if (path != null) { files.WriteBytes(path, bytes); Status = "Esportati tutti gli spettri TXT, progetto e istruzioni nello ZIP."; }
        }), () => IsSeismic && HasResults);
        InputsChanged();
    }
    public ModuleDocument Document() => new(1, module.Id, Fields.ToDictionary(f => f.Key, f => f.Value), "");
    /// <summary>Valida tutto prima di cambiare i campi: un file errato non sovrascrive il lavoro aperto.</summary>
    public void Load(ModuleDocument doc)
    {
        if (IsSnow || IsWind) doc = ClimateDocuments.Normalize(doc, module);
        // L'importazione dei vecchi materiali rende esplicite le ipotesi prima di modificare la vista.
        if (IsConcrete) doc = ((ConcreteWorkspace)module).Normalize(doc);
        module.Calculate(doc);
        loading = true;
        try { foreach (var field in Fields) field.Value = doc.Fields[field.Key]; }
        finally { loading = false; }
        InputsChanged(); Status = "Progetto caricato: ricalcolare.";
    }
    private void Calculate()
    {
        Invalidate();
        var doc = Document(); var results = module.Calculate(doc);
        if (results.Any(r => !double.IsFinite(r.Valore))) throw new ArgumentException("Risultati non finiti.");
        var series = module is IPlotWorkspace plot ? plot.Plot(doc, results) : [];
        var exposure = module is WindWorkspace wind ? wind.ExposurePlot(doc) : [];
        Results = results; Series = series; ExposureSeries = exposure; NotifyResults();
        Status = "Calcolo completato."; UpdateContext();
    }
    private void InputsChanged()
    {
        Invalidate(); UpdateContext();
        if (IsSnow && double.TryParse(ByKey["falda"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double angle) && double.IsFinite(angle))
            RoofAngle = Math.Clamp(angle, 0, 90);
        else RoofAngle = 0;
        Changed(nameof(RoofAngle));
        if (IsSeismic)
        {
            try {
                double lon = ModuleInput.Number(ByKey["lon"].Value), lat = ModuleInput.Number(ByKey["lat"].Value);
                if (lon is >= -180 and <= 180 && lat is >= -85 and <= 85) { location = new(lon, lat); Changed(nameof(Location)); }
            } catch (ArgumentException) { /* Durante la digitazione incompleta il marker conserva l’ultima posizione valida. */ }
        }
    }
    private void UpdateContext()
    {
        try
        {
            if (IsSnow)
            {
                var doc = Document();
                Context = "Zona utilizzata: " + ((SnowWorkspace)module).Zone(doc) +
                    ". Quota del sito da inserire: non è la quota del capoluogo. Provincie secondo NTC 2018; per confini variati usare la zona manuale. Carico verticale sulla proiezione orizzontale.";
            }
            else if (IsWind)
            {
                var zone = Enum.Parse<Vento.Vento.Zone>(ByKey["zona"].Value);
                var rough = Enum.Parse<Vento.Vento.ClassiRugosità>(ByKey["rugosita"].Value);
                var state = ((WindWorkspace)module).Read(Document());
                Context = new Vento.GestioneZonaGeografica(zone).Descrizione + ". Rugosità " + rough + ": " + new Vento.GestioneRugositàTerreno(rough).Descrizione +
                    ". Categoria utilizzata: " + state.Category + ". " +
                    (ByKey["modello"].Value == WindWorkspace.Current
                        ? "Categoria assegnata manualmente secondo Fig. 3.3.2 NTC 2018: non è dedotta dalla sola rugosità. Fascia storica ignorata."
                        : "Categoria dedotta dalle tabelle storiche; il selettore categoria NTC 2018 è ignorato.") +
                    " cp positivo = pressione; negativo = depressione. cp=1 e cd=1 sono ipotesi da verificare.";
            }
            else if (IsSeismic) Context = "Marker WGS84: clic per selezionare, trascina per spostare la mappa, rotella per zoom. Coordinate ammesse dal modulo: lon 6–19°, lat 36–48°. Il motore usa il reticolo storico; la mappa non certifica la copertura del reticolo. q si applica solo al progetto SLV orizzontale.";
            else Context = "Valori in MPa = N/mm². NTC 2018 §§11.2.10 e 4.1.2.1.1: fck = 0,83 Rck, diverso dal valore nominale nella sigla. Aggregati riciclati: occorre qualificare la miscela e rispettare origine e percentuali della Tab. 11.2.III. Ecm da prove sostituisce la stima; obbligatorio per maturazione a vapore. Le prescrizioni di durabilità EN 206 non sono verificate automaticamente."
                + (Enum.TryParse<Calcestruzzo.ClassiNormale>(ByKey["classe"].Value, out var cls) && new Calcestruzzo.Calcestruzzo(cls).Rck > 55 ? " Oltre C45/55: sperimentazione preventiva e controllo qualità (§4.1)." : "")
                + (ByKey["classe"].Value is "C80_95" or "C90_105" ? " Oltre C70/85: qualificazione secondo §11.1, caso C)." : "");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Context = ex.Message; }
        Changed(nameof(Context));
    }
    private void Invalidate()
    {
        Results = []; Series = []; ExposureSeries = []; NotifyResults(); Status = "Dati modificati: ricalcolare.";
    }
    private void NotifyResults()
    {
        foreach (var name in new[] { nameof(Results), nameof(Series), nameof(ExposureSeries), nameof(HasResults), nameof(SnowLoad), nameof(Summary) }) Changed(name);
        ExportCommand?.Refresh(); ExportSpectraCommand?.Refresh();
    }
    private void Run(Action action)
    {
        try { action(); } catch (Exception ex) { Invalidate(); Status = ex.Message; }
    }
}
