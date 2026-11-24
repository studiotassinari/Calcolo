using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using STA.Calcolo.Contracts;
using STA.Calcolo.IO;
using STA.Plinti.Core;
using STA.Integrations.Midas;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>Voce di un elenco a scelta: valore salvato nel JSON ed etichetta mostrata.</summary>
public sealed record FootingOption(string Value, string Label);

/// <summary>Campo di un parametro del plinto: chiave JSON del contratto, etichetta, unità, gruppo e valore testuale (virgola o punto).</summary>
public sealed class FootingField(string key, string label, string unit, string group, IReadOnlyList<FootingOption>? options = null) : ObservableObject
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public string Unit { get; } = unit;
    public string Group { get; } = group;
    public IReadOnlyList<FootingOption>? Options { get; } = options;
    public bool IsChoice => Options is not null;
    private string value = "";
    public string Value { get => value; set => Set(ref this.value, value ?? ""); }
}
public sealed record FootingFieldGroup(string Title, IReadOnlyList<FootingField> Fields);

/// <summary>Riga modificabile delle reazioni (kN, kN·m): FZ positiva comprime il plinto.</summary>
public sealed class ReactionRow : ObservableObject
{
    private int node = 1; private string @case = "ULS01";
    private double fx, fy, fz, mx, my, mz;
    public int Node { get => node; set => Set(ref node, value); }
    public string Case { get => @case; set => Set(ref @case, value ?? ""); }
    public double FX { get => fx; set => Set(ref fx, value); }
    public double FY { get => fy; set => Set(ref fy, value); }
    public double FZ { get => fz; set => Set(ref fz, value); }
    public double MX { get => mx; set => Set(ref mx, value); }
    public double MY { get => my; set => Set(ref my, value); }
    public double MZ { get => mz; set => Set(ref mz, value); }
    public NodeReaction ToContract() => new() { Node = Node, Case = Case.Trim(), FX = FX, FY = FY, FZ = FZ, MX = MX, MY = MY, MZ = MZ };
    public static ReactionRow From(NodeReaction r) => new() { Node = r.Node, Case = r.Case, FX = r.FX, FY = r.FY, FZ = r.FZ, MX = r.MX, MY = r.MY, MZ = r.MZ };
}

/// <summary>Riga della tabella degli esiti: valori principali e verifica più gravosa di una sestupla.</summary>
public sealed record FootingResultRow(FootingDesignResult Source, int Node, string Case, string N, string Ex, string Ey, string SigmaMax, string Contact,
    string Bearing, string Sliding, string AsX, string AsY, string Punching, string Governing, bool Ok, string Outcome);

/// <summary>Riga del dettaglio delle verifiche della sestupla selezionata.</summary>
public sealed record FootingCheckRow(string Name, string Demand, string Capacity, string Unit, string Ratio, bool Ok, string Note);

/// <summary>
/// Plinti da reazioni vincolari (MVVM): parametri raggruppati, reazioni modificabili, ricalcolo automatico con il motore rivisto
/// NTC 2018 / EC2 / EC7, dettaglio delle verifiche e disegno. Il progetto resta nel contratto JSON FootingRequest (schemaVersion 1);
/// i parametri nuovi sono facoltativi. Lettura da MIDAS GEN NX e import CSV invariati.
/// </summary>
public sealed class FootingViewModel : ObservableObject
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string N(double v, int d = 1) => double.IsFinite(v) ? v.ToString("N" + d, It) : "—";
    private readonly IFileService files;
    private bool loading;
    private long revision;
    private FootingRequest? lastInput;
    private IReadOnlyList<FootingDesignResult> design = [];

    private static readonly FootingOption[] LimitStates = [new("ULS", "SLU (Approccio 2)"), new("SLS", "SLE")];
    private static readonly FootingOption[] BearingModes = [new("assigned", "qRd assegnata"), new("computed", "Calcolata EC7 (γR)")];
    /// <summary>Definizione dei campi: chiave JSON, etichetta, unità e gruppo; l'ordine è quello della colonna dati.</summary>
    private static FootingField[] CreateFields() => [
        new("B", "Base X  B", "m", "Geometria"), new("L", "Base Y  L", "m", "Geometria"), new("h", "Spessore  h", "m", "Geometria"),
        new("cx", "Pilastro X  cx", "m", "Geometria"), new("cy", "Pilastro Y  cy", "m", "Geometria"),
        new("nodeHeight", "Quota del nodo sopra la base", "m", "Geometria"), new("depth", "Piano di posa da p.c. (0 = h)", "m", "Geometria"),
        new("limitState", "Combinazioni", "", "Terreno e verifiche", LimitStates), new("bearing", "Capacità portante", "", "Terreno e verifiche", BearingModes),
        new("qRd", "qRd assegnata (SLE: q ammissibile)", "kPa", "Terreno e verifiche"),
        new("phi", "φ′k", "°", "Terreno e verifiche"), new("cohesion", "c′k", "kPa", "Terreno e verifiche"), new("soilWeight", "γ terreno", "kN/m³", "Terreno e verifiche"),
        new("mu", "Attrito base μ = tan δ", "", "Terreno e verifiche"), new("gammaRBearing", "γR capacità portante", "", "Terreno e verifiche"), new("gammaRSliding", "γR scorrimento", "", "Terreno e verifiche"),
        new("fck", "fck", "MPa", "Calcestruzzo e armature"), new("fyk", "fyk", "MPa", "Calcestruzzo e armature"), new("alphaCC", "αcc", "", "Calcestruzzo e armature"),
        new("gammaC", "γc", "", "Calcestruzzo e armature"), new("gammaS", "γs", "", "Calcestruzzo e armature"), new("density", "Peso unitario cls", "kN/m³", "Calcestruzzo e armature"),
        new("cover", "Copriferro", "mm", "Calcestruzzo e armature"), new("diameter", "Diametro barre", "mm", "Calcestruzzo e armature"), new("spacing", "Passo barre (X e Y)", "mm", "Calcestruzzo e armature"),
        new("beta", "β punzonamento", "", "Calcestruzzo e armature"),
        new("gammaG", "γG pesi sfavorevoli", "", "Pesi"), new("gammaGFav", "γG pesi favorevoli", "", "Pesi"), new("extraWeight", "Altri pesi", "kN", "Pesi")];

    public FootingViewModel(IFileService files)
    {
        this.files = files;
        Parameters = CreateFields();
        Groups = Parameters.GroupBy(f => f.Group).Select(g => new FootingFieldGroup(g.Key, g.ToArray())).ToArray();
        foreach (var f in Parameters) f.PropertyChanged += (_, _) => Edited();
        Reactions.CollectionChanged += (_, e) =>
        {
            foreach (ReactionRow row in e.NewItems ?? Array.Empty<ReactionRow>()) row.PropertyChanged += (_, _) => Edited();
            Edited();
        };
        OpenCommand = new(() => Run(() => { var path = files.Open("Progetto JSON|*.json"); if (path != null) { SetProject(CalculationFiles.ReadRequest(files.Read(path))); Status = "Progetto aperto."; } }));
        SaveCommand = new(() => Run(() => { var input = GetProject(); _ = FootingDesignCalculator.Calculate(input.Reactions, input.Parameters); var path = files.Save("Progetto JSON|*.json", "plinti.json"); if (path != null) { files.Write(path, CalculationFiles.Write(input)); Status = "Progetto salvato."; } }));
        ImportCommand = new(() => Run(() => { var path = files.Open("Reazioni CSV|*.csv"); if (path != null) { var rows = CalculationFiles.ReadCsv(files.Read(path)); ReplaceReactions(rows); Status = "Reazioni importate in kN e kN·m."; } }));
        ExampleCommand = new(() => Run(() => { SetProject(CalculationFiles.ReadRequest(files.Read(System.IO.Path.Combine(AppContext.BaseDirectory, "plinti-example.json")))); Status = "Esempio caricato e calcolato."; }));
        CalculateCommand = new(() => Run(Recalculate));
        ExportCommand = new(() => Run(() =>
        {
            var path = files.Save("Risultati JSON|*.json", "risultati-plinti.json");
            // Esportati entrambi i motori: rivisto (usato dall'interfaccia) e prototipo (contratto storico della CLI).
            if (path != null) { files.Write(path, CalculationFiles.Write(new { input = lastInput, design, prototype = new FootingModule().Calculate(lastInput!) })); Status = "Risultati esportati."; }
        }), () => lastInput != null);
        AddReactionCommand = new(() => { var last = Reactions.LastOrDefault(); Reactions.Add(new ReactionRow { Node = last?.Node ?? 1, Case = $"ULS{Reactions.Count + 1:00}", FZ = last?.FZ ?? 0 }); });
        RemoveReactionCommand = new(() => { if (SelectedReaction is not null) Reactions.Remove(SelectedReaction); }, () => SelectedReaction is not null);
        DesignCommand = new(() => Run(FindMinimumBase), () => lastInput != null);
        ReadMidasCommand = new(ReadMidas, e => { Status = e.Message; });
        // Valori iniziali: esempio del contratto, con i parametri facoltativi ai valori predefiniti.
        try { SetProject(CalculationFiles.ReadRequest(files.Read(System.IO.Path.Combine(AppContext.BaseDirectory, "plinti-example.json")))); Status = "Esempio caricato. Modificare i dati o importare le reazioni."; }
        catch (Exception ex) when (ex is System.IO.IOException or ArgumentException or System.IO.InvalidDataException or JsonException) { Status = "Aprire un progetto oppure caricare l'esempio."; }
    }

    // ── Stato esposto ───────────────────────────────────────────────────────
    private string status = "";
    public string Status { get => status; private set => Set(ref status, value); }
    private string inputError = "";
    public string InputError { get => inputError; private set { if (Set(ref inputError, value)) Changed(nameof(InputsValid)); } }
    public bool InputsValid => InputError.Length == 0;
    public IReadOnlyList<FootingField> Parameters { get; }
    public IReadOnlyList<FootingFieldGroup> Groups { get; }
    public ObservableCollection<ReactionRow> Reactions { get; } = [];
    private ReactionRow? selectedReaction;
    public ReactionRow? SelectedReaction { get => selectedReaction; set { if (Set(ref selectedReaction, value)) RemoveReactionCommand.Refresh(); } }
    public IReadOnlyList<FootingResultRow> Results { get; private set; } = [];
    private FootingResultRow? selectedResult;
    /// <summary>Riga degli esiti selezionata: alimenta il dettaglio delle verifiche e il disegno.</summary>
    public FootingResultRow? SelectedResult { get => selectedResult; set { if (Set(ref selectedResult, value)) NotifySelection(); } }
    public IReadOnlyList<FootingCheckRow> SelectedChecks => SelectedResult?.Source.Checks.Select(c => new FootingCheckRow(c.Name, N(c.Demand, 3), N(c.Capacity, 3), c.Unit,
        c.Ratio is double r ? N(r, 2) : "—", c.Ok, c.Note)).ToArray() ?? [];
    public IReadOnlyList<string> SelectedIssues => SelectedResult?.Source.Issues ?? [];
    public FootingDesignResult? SelectedDesign => SelectedResult?.Source;
    /// <summary>Parametri validi correnti (per il disegno anche senza risultati).</summary>
    public FootingParameters? CurrentParameters { get; private set; }
    public string Summary => Results.Count == 0 ? (InputsValid ? "Inserire almeno una reazione." : InputError)
        : Results.All(r => r.Ok) ? $"Tutte le {Results.Count} combinazioni verificate." : $"{Results.Count(r => !r.Ok)} combinazioni su {Results.Count} non verificate.";
    public bool AllOk => Results.Count > 0 && Results.All(r => r.Ok);
    public string Scope => FootingDesignCalculator.Scope;

    // MIDAS GEN NX: credenziali solo in memoria.
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string NodeIds { get; set; } = "";
    public string Cases { get; set; } = "";
    public bool AnalysisReady { get; set; }

    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ExampleCommand { get; }
    public RelayCommand CalculateCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand AddReactionCommand { get; }
    public RelayCommand RemoveReactionCommand { get; }
    /// <summary>Cerca la base quadrata minima (passo 5 cm) che verifica tutte le combinazioni e la applica ai campi B e L.</summary>
    public RelayCommand DesignCommand { get; }
    public AsyncCommand ReadMidasCommand { get; }

    // ── Progetto ────────────────────────────────────────────────────────────
    public void SetProject(FootingRequest request)
    {
        var values = JsonSerializer.SerializeToElement(request.Parameters);
        loading = true;
        try
        {
            foreach (var field in Parameters)
                if (values.TryGetProperty(field.Key, out var v))
                    field.Value = v.ValueKind == JsonValueKind.Number ? v.GetDouble().ToString("G", It) : v.ToString();
            ReplaceReactions(request.Reactions);
        }
        finally { loading = false; }
        Recalculate();
    }
    private void ReplaceReactions(IEnumerable<NodeReaction> rows)
    {
        var wasLoading = loading; loading = true;
        try { Reactions.Clear(); foreach (var r in rows) Reactions.Add(ReactionRow.From(r)); }
        finally { loading = wasLoading; }
        if (!loading) Recalculate();
    }

    /// <summary>Costruisce il contratto dai campi; numeri con virgola o punto. Errore esplicito sul campo non valido.</summary>
    public FootingRequest GetProject()
    {
        var map = new Dictionary<string, object>();
        foreach (var f in Parameters)
        {
            if (f.IsChoice) { map[f.Key] = f.Value.Trim(); continue; }
            if (!double.TryParse(f.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v))
                throw new ArgumentException($"{f.Label}: inserire un numero.");
            map[f.Key] = v;
        }
        var parameters = JsonSerializer.Deserialize<FootingParameters>(JsonSerializer.Serialize(map))!;
        return new() { SchemaVersion = 1, Parameters = parameters, Reactions = Reactions.Select(r => r.ToContract()).ToArray() };
    }

    private void Edited() { if (loading) return; revision++; Recalculate(); }

    /// <summary>Ricalcolo completo: convalida, motore rivisto, tabella degli esiti e selezione conservata per nodo e caso.</summary>
    private void Recalculate()
    {
        var previous = SelectedResult is { } s ? (s.Node, s.Case) : ((int, string)?)null;
        lastInput = null; design = []; Results = []; CurrentParameters = null;
        try
        {
            var input = GetProject();
            FootingDesignCalculator.Validate(input.Parameters); CurrentParameters = input.Parameters;
            if (input.Reactions.Count > 0)
            {
                design = FootingDesignCalculator.Calculate(input.Reactions, input.Parameters);
                lastInput = input;
                Results = design.Select(Row).ToArray();
            }
            InputError = ""; Status = Results.Count > 0 ? "Verifiche aggiornate." : "Inserire almeno una reazione.";
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.InvalidDataException or JsonException or InvalidOperationException) { InputError = ex.Message; Status = ex.Message; }
        foreach (var name in new[] { nameof(Results), nameof(Summary), nameof(AllOk), nameof(CurrentParameters) }) Changed(name);
        SelectedResult = Results.FirstOrDefault(r => previous is { } p && r.Node == p.Item1 && r.Case == p.Item2) ?? Results.FirstOrDefault(r => !r.Ok) ?? Results.FirstOrDefault();
        NotifySelection();
        ExportCommand.Refresh(); DesignCommand.Refresh();
    }
    private void NotifySelection() { foreach (var name in new[] { nameof(SelectedChecks), nameof(SelectedIssues), nameof(SelectedDesign) }) Changed(name); }

    private static FootingResultRow Row(FootingDesignResult r)
    {
        string Ratio(string name) => r.Checks.FirstOrDefault(c => c.Name == name) is { } c ? (c.Ratio is double v ? N(v, 2) : "—") : "";
        var g = r.Governing;
        return new(r, r.Node, r.Case, N(r.N), N(r.Ex, 3), N(r.Ey, 3), r.Pressure.Valid ? N(r.Pressure.SigmaMax) : "—",
            r.Pressure.Valid ? (r.Pressure.ContactRatio * 100).ToString("0", It) + " %" : "—",
            Ratio("Capacità portante"), Ratio("Scorrimento"),
            r.LimitState == "ULS" ? $"{N(r.AsReqX, 0)} / {N(r.AsProvided, 0)}" : "", r.LimitState == "ULS" ? $"{N(r.AsReqY, 0)} / {N(r.AsProvided, 0)}" : "",
            Ratio("Punzonamento"), g is null ? "—" : $"{g.Name} ({N(g.Ratio!.Value, 2)})", r.Ok, r.Ok ? "Verificato" : "NON verificato");
    }

    /// <summary>Base quadrata minima a passi di 5 cm (fino a 3 volte il valore corrente) che soddisfa tutte le combinazioni.</summary>
    private void FindMinimumBase()
    {
        var input = GetProject();
        var start = Math.Max(Math.Max(input.Parameters.Cx, input.Parameters.Cy) + .2, .5);
        for (var b = Math.Ceiling(start / .05) * .05; b <= 3 * Math.Max(input.Parameters.B, input.Parameters.L) + 1e-9; b += .05)
        {
            var q = input.Parameters with { B = Math.Round(b, 2), L = Math.Round(b, 2) };
            try { if (FootingDesignCalculator.Calculate(input.Reactions, q).All(r => r.Ok)) { SetBase(q.B); Status = $"Base minima che verifica tutte le combinazioni: {N(q.B, 2)} × {N(q.L, 2)} m."; return; } }
            catch (ArgumentException) { /* geometria non ammessa per questa dimensione: si prosegue */ }
        }
        Status = "Nessuna base quadrata fino a 3 volte la dimensione attuale verifica tutte le combinazioni: aumentare lo spessore o rivedere i dati.";
    }
    private void SetBase(double b)
    {
        loading = true;
        try { Parameters.Single(f => f.Key == "B").Value = b.ToString("G", It); Parameters.Single(f => f.Key == "L").Value = b.ToString("G", It); }
        finally { loading = false; }
        Recalculate();
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is ArgumentException or System.IO.InvalidDataException or JsonException or InvalidOperationException or System.IO.IOException or FormatException) { Status = e.Message; }
    }

    private async Task ReadMidas()
    {
        if (!AnalysisReady) throw new InvalidOperationException("Confermare risultati aggiornati e concomitanti.");
        var started = revision;
        var nodes = NodeIds.Split([' ', ',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        var cases = Cases.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray();
        using var source = new MidasReactionSource(BaseUrl, ApiKey); var result = await source.ReadAsync(new(nodes, cases));
        if (started != revision) { Status = "Importazione ignorata: progetto modificato durante la richiesta."; return; }
        ReplaceReactions(result.Rows); Status = $"Importate {Reactions.Count} sestuple kN/m. Confrontare con le tabelle di GEN NX.";
    }
}
