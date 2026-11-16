using System.Globalization;
using STA.Taglio.Core;
namespace STA.Calcolo.Reporting;

public static class ShearReportBuilder
{
    public static ReportDocument Build(ShearInput p, string title, DateTime created)
    {
        var r = ShearCalculator.Calculate(p);
        static string N(double n) => n.ToString("N3", CultureInfo.GetCultureInfo("it-IT"));
        string[] Row(string label, double value, string unit = "") => [label, N(value) + " " + unit];
        var geometry = new ReportTable("Geometria e azioni", ["Parametro", "Valore"],
        [
            ["Tipologia", p.Wall ? "Parete · sezione in pianta" : "Trave · sezione trasversale"],
            Row("H", p.H, "mm"), Row("b / spessore", p.B, "mm"), Row("Distanza al baricentro armatura tesa", p.Cover, "mm"),
            Row("d = H − distanza armatura", r.D, "mm"), Row("Asl", p.Asl, "mm²"),
            Row("N (positivo a compressione)", p.N, "kN"), Row("|VEd|", p.V, "kN")
        ]);
        var materials = new ReportTable("Materiali e modello", ["Parametro", "Valore"],
        [
            ["Modello originale", p.Eurocode ? "EN 1992" : "NTC 2018"],
            Row("fck", p.Fck, "MPa"), Row("fyk", p.Fyk, "MPa"), Row("γc / γs", p.GammaC, "/ " + N(p.GammaS)),
            Row("αcc", p.AlphaCc), Row("fcd / fyd", r.Fcd, "/ " + N(r.Fyd) + " MPa"),
            Row("θ", p.Theta, "°"), Row("Fattore bielle parete", r.WallFactor)
        ]);
        var reinforcement = new ReportTable("Armature a taglio", ["Parametro", "Valore"],
        [
            Row("Bracci staffe", p.Legs), Row("Diametro / interasse staffe", p.Diameter, "/ " + N(p.Spacing) + " mm"), Row("α staffe", p.Alpha, "°"),
            Row("Numero piegati", p.BentCount), Row("Diametro / interasse piegati", p.BentDiameter, "/ " + N(p.BentSpacing) + " mm"), Row("α piegati", p.BentAlpha, "°")
        ]);
        var results = new ReportTable("Resistenze allo stesso θ di progetto", ["Grandezza", "Valore"],
        [
            Row("VRd,c · senza armatura a taglio", r.Concrete, "kN"),
            Row("VRd,s · staffe + piegati", r.Reinforcement, "kN"),
            Row("VRd,max · bielle (riduzione parete già applicata)", r.Strut, "kN"),
            Row("VRd · configurazione attuale", r.Capacity, "kN"),
            ["VEd / VRd", r.Utilization is double u ? N(u) : "Non definito: capacità nulla"],
            ["Confronto della sola resistenza", r.Pass ? "Sufficiente" : "Insufficiente"],
            Row("θ proposto", r.SuggestedTheta, "°"), Row("VRd al θ proposto", r.SuggestedCapacity, "kN")
        ]);
        return new(title, created,
        [
            new("Taglio · dati della sezione", [geometry, materials], [], [title, "Dimensioni in mm, aree in mm², resistenze in MPa, forze in kN. Per pareti H è la lunghezza resistente in pianta, non l’altezza di piano."]),
            new("Taglio · armature e risultati", [reinforcement, results], [], [
                "Con armatura VRd = min(VRd,s; VRd,max); senza staffe e piegati si espone VRd,c.",
                "Formule migrate da TaglioClsRect. Campo di ricerca θ: 22,5°–45°; proposta per pareti NTC: 45°. Il fattore 0,40 è applicato una sola volta alle bielle della parete.",
                "La migrazione non aggiorna la normativa. Conserva le varianti del codice originale, incluso σcp = N/(b·d) per VRd,c e la riduzione EN delle bielle. Non verifica dettagli costruttivi, flessione, duttilità o tutte le prescrizioni sismiche. VEd deve essere già di progetto.",
                "I risultati esportati corrispondono agli input indicati; le modifiche al documento Word non ricalcolano il progetto."
            ], "135F6E")
        ]);
    }
}
