using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
using STA.Taglio.Core;
namespace STA.Calcolo.Desktop.ViewModels;

public sealed class ShearViewModel : ObservableObject
{
    private readonly ShearWorkspace module = new();
    private readonly IFileService files;
    private readonly IShearReportService reports;
    private bool loading;
    public IReadOnlyDictionary<string, ShearNumberField> ByKey { get; }
    public IReadOnlyList<ShearNumberField> Geometry { get; }
    public IReadOnlyList<ShearNumberField> Loads { get; }
    public IReadOnlyList<ShearNumberField> Materials { get; }
    public IReadOnlyList<ShearNumberField> Stirrups { get; }
    public IReadOnlyList<ShearNumberField> BentBars { get; }
    public string[] Standards { get; } = ["NTC 2018", "EN 1992"];
    public string[] Kinds { get; } = ["Trave", "Parete"];
    private string standard = "NTC 2018", kind = "Trave", title = "Verifica a taglio";
    public string Standard
    {
        get => standard;
        set { if (Set(ref standard, value)) { if (!loading) ByKey["AlphaCc"].Value = value == "EN 1992" ? "1" : "0.85"; Invalidate(); } }
    }
    public string Kind { get => kind; set { if (Set(ref kind, value)) { Changed(nameof(FigureCaption)); Invalidate(); } } }
    public string Title { get => title; set { if (Set(ref title, value)) Invalidate(); } }
    public string FigureCaption => Kind == "Parete" ? "Parete · sezione in pianta" : "Trave · sezione trasversale";
    public string MethodNote => "Modello TaglioClsRect: θ tra 22,5° e 45°; bielle parete × 0,40. La proposta NTC per pareti usa θ = 45°. Verifica della sola resistenza a taglio.";
    private string status = "Inserire i dati; le quote sul disegno sono modificabili.";
    public string Status { get => status; private set => Set(ref status, value); }
    public ShearFigure Figure { get; private set; } = ShearFigure.Empty;
    public bool HasFigure { get; private set; }
    public bool InputsValid => ByKey.Values.All(f => f.IsValid);
    public ShearResult? Result { get; private set; }
    public IReadOnlyList<ResultRow> Results { get; private set; } = [];
    public bool HasResults => Result is not null;
    public bool Passed => Result?.Pass == true;
    private static string N(double value) => value.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));
    public string CapacityText => Result is null ? "—" : N(Result.Capacity) + " kN";
    public string ConcreteText => Result is null ? "—" : N(Result.Concrete) + " kN";
    public string UtilizationText => Result is null ? "Da calcolare" : Result.Utilization is double u ? N(u * 100) + " %" : "Capacità nulla";
    public string Outcome => Result is null ? "Dati da verificare" : Result.Pass ? "Resistenza a taglio sufficiente" : "Resistenza a taglio insufficiente";
    public string SuggestedText => Result is null ? "—" : N(Result.SuggestedTheta) + "° · " + N(Result.SuggestedCapacity) + " kN";
    public RelayCommand CalculateCommand { get; }
    public RelayCommand SuggestCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand WordCommand { get; }

    public ShearViewModel(IFileService files, IShearReportService? reports = null)
    {
        this.files = files; this.reports = reports ?? new ShearReportService();
        ByKey = module.Fields.Where(f => f.Choices == null).Select(f => new ShearNumberField(f)).ToDictionary(f => f.Key);
        IReadOnlyList<ShearNumberField> Group(params string[] keys) => keys.Select(k => ByKey[k]).ToArray();
        Geometry = Group("H", "B", "Cover", "Asl");
        Loads = Group("N", "V");
        Materials = Group("Fck", "Fyk", "GammaC", "GammaS", "AlphaCc");
        Stirrups = Group("Diameter", "Spacing", "Legs", "Alpha");
        BentBars = Group("BentDiameter", "BentSpacing", "BentCount", "BentAlpha");
        foreach (var f in ByKey.Values) f.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ShearNumberField.Value)) Invalidate(); };
        CalculateCommand = new(() => Run(Calculate), () => InputsValid);
        SuggestCommand = new(() => Run(() =>
        {
            var result = module.Compute(Document());
            ByKey["Theta"].Value = result.SuggestedTheta.ToString("G17", CultureInfo.InvariantCulture);
            Calculate();
            Status = "θ proposto applicato; risultati ricalcolati allo stesso angolo.";
        }), () => InputsValid);
        OpenCommand = new(() => Run(() =>
        {
            var path = files.Open("Progetto taglio|*.json;*.tgl");
            if (path != null) Load(ShearFiles.Read(files.Read(path)));
        }));
        SaveCommand = new(() => Run(() =>
        {
            var content = ShearFiles.Write(Document());
            var path = files.Save("Progetto taglio JSON|*.json", "taglio-cls.json");
            if (path != null) { files.Write(path, content); Status = "Progetto salvato."; }
        }), () => InputsValid);
        ExportCommand = new(() => Run(() =>
        {
            var path = files.Save("Risultati taglio JSON|*.json", "taglio-cls-risultati.json");
            if (path != null) files.Write(path, ModuleFiles.Write(new { Input = Document(), Result }));
        }), () => HasResults);
        WordCommand = new(() => Run(() =>
        {
            var path = files.Save("Relazione taglio Word|*.docx", "taglio-cls-relazione.docx");
            if (path != null) { files.WriteBytes(path, this.reports.Word(Document())); Status = "Relazione Word creata."; }
        }), () => HasResults);
        UpdateFigure();
    }
    public ModuleDocument Document()
    {
        var fields = ByKey.ToDictionary(k => k.Key, k => k.Value.Value);
        fields["Standard"] = Standard; fields["Kind"] = Kind;
        return new(1, module.Id, fields, Title);
    }
    public void Load(ModuleDocument doc)
    {
        module.Input(doc); // Validate completely before touching the open project.
        loading = true;
        try
        {
            Standard = doc.Fields["Standard"]; Kind = doc.Fields["Kind"]; Title = doc.Data;
            foreach (var (key, field) in ByKey) field.Value = doc.Fields[key];
        }
        finally { loading = false; }
        Invalidate(); Status = "Progetto caricato. Premere Calcola.";
    }
    private void Calculate()
    {
        Invalidate();
        Result = module.Compute(Document()); Results = ShearWorkspace.Rows(Result);
        NotifyResults(); Status = "Calcolo aggiornato. " + Outcome + ".";
    }
    private void Invalidate()
    {
        if (loading) return;
        Result = null; Results = []; NotifyResults(); UpdateFigure();
        Changed(nameof(InputsValid));
        CalculateCommand?.Refresh(); SuggestCommand?.Refresh(); SaveCommand?.Refresh();
        Status = InputsValid ? "Dati modificati: premere Calcola." : "Correggere i campi numerici evidenziati.";
    }
    private void NotifyResults()
    {
        foreach (var name in new[] { nameof(Result), nameof(Results), nameof(HasResults), nameof(Passed), nameof(CapacityText),
            nameof(ConcreteText), nameof(UtilizationText), nameof(Outcome), nameof(SuggestedText) }) Changed(name);
        ExportCommand?.Refresh(); WordCommand?.Refresh();
    }
    private void UpdateFigure()
    {
        HasFigure = ByKey["B"].TryNumber(out var b) && b >= 1 && b <= 100000
            && ByKey["H"].TryNumber(out var h0) && h0 >= 1 && h0 <= 100000;
        // Read separately so the variables remain definitely assigned after short-circuit evaluation.
        ByKey["H"].TryNumber(out var h);
        HasFigure &= ByKey["Cover"].TryNumber(out var cover) && cover >= 0 && cover < h;
        if (HasFigure) Figure = ShearFigure.Create(b, h, cover); // Keep editor anchors while the user clears a quote.
        Changed(nameof(Figure)); Changed(nameof(HasFigure));
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        { Invalidate(); Status = e.Message; }
    }
}
