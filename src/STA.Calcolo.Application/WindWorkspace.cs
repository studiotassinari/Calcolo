using Vento;
namespace STA.Calcolo.Application;

/// <summary>Pressione e profili vento: modello NTC 2018 esplicito e compatibilità dei progetti storici.</summary>
public sealed class WindWorkspace : ICalculationWorkspace, IPlotWorkspace
{
    public const string Legacy = "Storico NTC 2008";
    public const string Current = "NTC 2018";
    public string Id => "vento";
    public string Title => "Carico vento";
    public string Description => "Profilo p(z) = qr · ce(z) · cp · cd. TR = 50 anni. Cp e cd assegnati dal progettista; azioni tangenti e aeroelastiche escluse.";
    public IReadOnlyList<ModuleField> Fields => [
        new("modello", "Modello di calcolo", Current, [Current, Legacy]),
        new("zona", "Zona geografica", "Zona2", Enum.GetNames<Vento.Vento.Zone>()),
        new("rugosita", "Classe di rugosità del terreno", "C", Enum.GetNames<Vento.Vento.ClassiRugosità>()),
        new("distanza", "Fascia costiera (solo modello storico)", "TerraMenoDi10km", Enum.GetNames<Vento.Vento.DistanzeDaCosta>()),
        new("categoria", "Categoria NTC 2018 (Fig. 3.3.2)", "III", WindProfile.Categories.Select(c => c.Category).ToArray()),
        new("quota", "Quota sito [m s.l.m.]", "100"), new("altezza", "Altezza struttura [m]", "10"),
        new("ct", "Coefficiente topografico ct [1–5]", "1"), new("cp", "Coefficiente di pressione cp (con segno)", "1"),
        new("cd", "Coefficiente dinamico cd", "1")];
    public string DataHint => "";
    public string ExampleData => "";
    public string PlotCaption => "Pressione p(z) [N/m²] — quota z [m]";
    public bool EqualAxes => false;
    public sealed record State(double Velocity, double Qr, string Category, double Height, double Ct, double Cp, double Cd);
    public State Read(ModuleDocument document)
    {
        var doc = ClimateDocuments.Normalize(document, this); var i = new ModuleInput(doc, this);
        var zone = i.Choice<Vento.Vento.Zone>("zona");
        double altitude = i.Number("quota", 0, 1500), h = i.Number("altezza", .01, 200);
        double velocity; string category;
        if (i.Text("modello") == Legacy)
        {
            var old = new Vento.Vento(zone) { AltezzaGeografica = altitude, AltezzaStruttura = h };
            old.RugositàTerreno = new(i.Choice<Vento.Vento.ClassiRugosità>("rugosita"));
            old.DistanzaDaCosta = i.Choice<Vento.Vento.DistanzeDaCosta>("distanza");
            if (old.ClasseEsposizione == null) throw new ArgumentException("Combinazione storica di zona, rugosità e fascia non prevista.");
            velocity = old.VelocitàRiferimento; category = old.ClasseEsposizione.CategoriaEsposizione.ToString();
        }
        else { velocity = WindProfile.BaseVelocity(zone, altitude); category = i.Text("categoria"); }
        return new(velocity, .625 * velocity * velocity, category, h, i.Number("ct", 1, 5), i.Number("cp", -10, 10), i.Number("cd", .01, 10));
    }
    public IReadOnlyList<ResultRow> Calculate(ModuleDocument document)
    {
        var s = Read(document); double ce = WindProfile.Exposure(s.Category, s.Height, s.Ct);
        var p = WindProfile.Categories.Single(c => c.Category == s.Category);
        return [new("Velocità riferimento", s.Velocity, "m/s"), new("Pressione riferimento qr", s.Qr, "N/m²"),
            new("Ce in sommità", ce, ""), new("Pressione p(H)", s.Qr * ce * s.Cp * s.Cd, "N/m²"),
            new("kr", p.Kr, ""), new("z0", p.Z0, "m"), new("zmin", p.Zmin, "m")];
    }
    /// <summary>Campiona anche zmin esatto, così il grafico conserva la transizione del plateau.</summary>
    private static double[] Heights(double h, string category) => Enumerable.Range(0, 101).Select(n => h * n / 100)
        .Append(Math.Min(h, WindProfile.Categories.Single(c => c.Category == category).Zmin)).Distinct().Order().ToArray();
    public IReadOnlyList<PlotSeries> Plot(ModuleDocument document, IReadOnlyList<ResultRow> results)
    {
        var s = Read(document);
        return [new("p(z)", Heights(s.Height, s.Category).Select(z => new PlotPoint(s.Qr * WindProfile.Exposure(s.Category, z, s.Ct) * s.Cp * s.Cd, z)).ToArray())];
    }
    public IReadOnlyList<PlotSeries> ExposurePlot(ModuleDocument document)
    {
        var s = Read(document);
        return WindProfile.Categories.Select(c => new PlotSeries(c.Category + (c.Category == s.Category ? " · scelta" : ""),
            Heights(s.Height, c.Category).Select(z => new PlotPoint(WindProfile.Exposure(c.Category, z, s.Ct), z)).ToArray())).ToArray();
    }
}
