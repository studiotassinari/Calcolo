using STA.Carichi.Neve;
namespace STA.Calcolo.Application;

/// <summary>Calcolo neve a singola falda: provincia, quota sito e carico sulla proiezione orizzontale.</summary>
public sealed class SnowWorkspace : ICalculationWorkspace, IPlotWorkspace
{
    public string Id => "neve";
    public string Title => "Carico neve";
    public string Description => "NTC 2018 §3.4 · singola falda, TR 50 anni, Ct = 1. Accumuli e coperture multiple esclusi.";
    public IReadOnlyList<ModuleField> Fields { get; }
    public SnowWorkspace()
    {
        var m = new CaricoNeve();
        Fields = [new("provincia", "Provincia (denominazioni NTC 2018)", SnowGeography.Manual, SnowGeography.Provinces),
            new("zona", "Zona manuale (ignorata con provincia)", m.ListaZone[0].Nome, m.ListaZone.Select(z => z.Nome).ToArray()),
            new("topografia", "Esposizione della copertura", "Normale", m.ListaClassiTopografia.Select(t => t.Topografia).ToArray()),
            new("quota", "Quota del sito [m s.l.m.]", "100"), new("falda", "Inclinazione falda [°]", "20"),
            new("scorrimento", "Scivolamento della neve", "Libero", ["Libero", "Ostacolato"])];
    }
    public string DataHint => "";
    public string ExampleData => "";
    public string PlotCaption => "Carico al suolo qsk [kN/m²] in funzione della quota [m s.l.m.]";
    public bool EqualAxes => false;
    public string Zone(ModuleDocument doc) => doc.Fields["provincia"] == SnowGeography.Manual ? doc.Fields["zona"] : SnowGeography.Zone(doc.Fields["provincia"]);
    public IReadOnlyList<ResultRow> Calculate(ModuleDocument document)
    {
        var doc = ClimateDocuments.Normalize(document, this);
        var i = new ModuleInput(doc, this); var m = new CaricoNeve();
        m.ZonaScelta = m.ListaZone.Single(z => z.Nome == Zone(doc));
        m.ClasseTopografiaScelta = m.ListaClassiTopografia.Single(t => t.Topografia == i.Text("topografia"));
        m.AltezzaTopografia = i.Number("quota", 0, 1500); m.InclinazioneFalda = i.Number("falda", 0, 90);
        // Parapetti e ostacoli impediscono la riduzione di μ sotto 0,8 (§3.4.3.1).
        double mu = i.Text("scorrimento") == "Ostacolato" ? Math.Max(.8, m.CoefficienteM1) : m.CoefficienteM1;
        return [new("qsk", m.Qsk, "kN/m²"), new("μ1", mu, ""), new("Ce", m.ClasseTopografiaScelta.Ce, ""),
            new("qs (Ct = 1)", m.Qsk * mu * m.ClasseTopografiaScelta.Ce, "kN/m²")];
    }
    public IReadOnlyList<PlotSeries> Plot(ModuleDocument document, IReadOnlyList<ResultRow> results)
    {
        var doc = ClimateDocuments.Normalize(document, this);
        var zone = new CaricoNeve().ListaZone.Single(z => z.Nome == Zone(doc));
        return [new("qsk", Enumerable.Range(0, 151).Select(n => new PlotPoint(n * 10, zone.ValoreCaratteristico(n * 10))).ToArray()),
            new("Sito", [new(ModuleInput.Number(doc.Fields["quota"]), results[0].Valore)], Markers: true)];
    }
}
