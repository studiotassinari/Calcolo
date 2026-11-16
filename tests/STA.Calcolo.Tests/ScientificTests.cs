using System.Globalization;
using System.IO.Compression;
using STA.Calcolo.Application;
using Vento;

/// <summary>Verifiche indipendenti dei nuovi contratti e delle correzioni rispetto al codice storico.</summary>
internal static class ScientificTests
{
    static ModuleDocument Example(ICalculationWorkspace m) => new(1, m.Id, m.Fields.ToDictionary(f => f.Key, f => f.Default), "");
    static void Assert(bool ok) { if (!ok) throw new Exception("Test scientifico fallito."); }
    static void Near(double actual, double expected, double tolerance = 1e-8) { Assert(double.IsFinite(actual) && Math.Abs(actual - expected) < tolerance); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Input errato accettato."); }
    public static void Run(Action<string, Action> test)
    {
        test("neve: tutte le province e casi geografici indipendenti", () => {
            Assert(SnowGeography.Provinces.Distinct().Count() == SnowGeography.Provinces.Length);
            Assert(SnowGeography.Provinces.Length == 111);
            foreach (var p in SnowGeography.Provinces.Skip(1)) Assert(new STA.Carichi.Neve.CaricoNeve().ListaZone.Any(z => z.Nome == SnowGeography.Zone(p)));
            Assert(SnowGeography.Zone("Ravenna") == "Zona I - Mediterranea");
            Assert(SnowGeography.Zone("Ferrara") == "Zona II");
            Assert(SnowGeography.Zone("Belluno") == "Zona I - Alpina");
            Assert(SnowGeography.Zone("Roma") == "Zona III");
            var m = new SnowWorkspace(); var d = Example(m); d.Fields["provincia"] = "Ravenna";
            Near(m.Calculate(d)[3].Valore, 1.2);
            d.Fields["quota"] = "600"; Near(m.Calculate(d)[0].Valore, 1.35 * (1 + Math.Pow(600d / 602, 2)));
            d.Fields["falda"] = "65"; Near(m.Calculate(d)[3].Valore, 0);
            d.Fields["scorrimento"] = "Ostacolato"; Near(m.Calculate(d)[1].Valore, .8);
            d.Fields["quota"] = "1501"; Reject(() => m.Calculate(d));
        });
        test("neve: migrazione contratto storico senza variazione dei valori", () => {
            var m = new SnowWorkspace(); var d = Example(m);
            d.Fields.Remove("provincia"); d.Fields.Remove("scorrimento");
            Near(m.Calculate(d)[3].Valore, 1.2);
            d.Fields.Remove("quota"); Reject(() => m.Calculate(d));
        });
        test("vento NTC 2018: velocità e plateau di tutte le categorie", () => {
            Near(WindProfile.BaseVelocity(Vento.Vento.Zone.Zona2, 750), 25);
            Near(WindProfile.BaseVelocity(Vento.Vento.Zone.Zona2, 1500), 36.25);
            Near(WindProfile.BaseVelocity(Vento.Vento.Zone.Zona3, 1000), 36.99);
            foreach (var c in WindProfile.Categories) {
                double log = Math.Log(c.Zmin / c.Z0), expected = c.Kr * c.Kr * log * (7 + log);
                Near(WindProfile.Exposure(c.Category, 0), expected);
                Near(WindProfile.Exposure(c.Category, c.Zmin), expected);
                Assert(WindProfile.Exposure(c.Category, 200) > expected);
                Near(WindProfile.Exposure(c.Category, 0, 1.2), c.Kr * c.Kr * 1.2 * log * (7 + 1.2 * log));
            }
            Reject(() => WindProfile.Exposure("III", double.NaN));
            Reject(() => WindProfile.Exposure("III", 201));
            Reject(() => WindProfile.BaseVelocity(Vento.Vento.Zone.Zona1, 1501));
        });
        test("vento: segno pressione, profili e importazione storica", () => {
            var m = new WindWorkspace(); var d = Example(m); var positive = m.Calculate(d)[3].Valore;
            d.Fields["cp"] = "-0,8"; Near(m.Calculate(d)[3].Valore, -.8 * positive);
            var series = m.Plot(d, m.Calculate(d))[0]; Near(series.Points.Last().X, -.8 * positive); Near(series.Points.Last().Y, 10);
            Assert(m.ExposurePlot(d).Count == 5); Assert(m.ExposurePlot(d).Count(s => s.Name.Contains("scelta")) == 1);
            var old = new ModuleDocument(1, "vento", new() { ["zona"]="Zona2", ["rugosita"]="C", ["distanza"]="TerraMenoDi10km", ["quota"]="1500", ["altezza"]="10" }, "");
            Near(m.Calculate(old)[0].Valore, 36.25);
            Assert(ClimateDocuments.Normalize(old, m).Fields["modello"] == WindWorkspace.Legacy);
            // Zona 3: il coefficiente altimetrico NTC 2018 differisce da quello del modello storico.
            old.Fields["zona"]="Zona3"; old.Fields["quota"]="1000"; Near(m.Calculate(old)[0].Valore, 37);
        });
        test("sisma: q iniziale, esportazione completa e unità indipendenti dalla lingua", () => {
            var m = new SeismicWorkspace(); var d = Example(m); Assert(d.Fields["q"] == "1.5");
            d.Fields["periodo"] = "0.1"; d.Fields["modo"] = "Progetto SLV"; d.Fields["direzione"] = "Verticale";
            var culture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
                var files = SeismicTextExport.Files(d);
                Assert(files.Keys.Count(k => k.EndsWith(".txt") && k != "LEGGIMI.txt") == 9);
                foreach (var file in files.Where(p => p.Key.EndsWith(".txt") && p.Key != "LEGGIMI.txt")) {
                    var rows = file.Value.Split('\n').Where(l => l.Length > 0 && !l.StartsWith('#')).ToArray();
                    Assert(rows.Length == 40);
                    double last = double.Parse(rows.Last().Split('\t')[0], CultureInfo.InvariantCulture); Assert(last > .1);
                    Assert(file.Value.Contains("m/s^2"));
                }
                using var zip = new ZipArchive(new MemoryStream(SeismicTextExport.Zip(d))); Assert(zip.Entries.Count == files.Count);
                var elastic = files["Elastico_SLV_H.txt"];
                d.Fields["q"] = "3"; var changed = SeismicTextExport.Files(d);
                // Le intestazioni cambiano con q, ma le ordinate elastiche devono rimanere identiche.
                string Data(string t) => string.Join("\n", t.Split('\n').Where(l => !l.StartsWith('#')));
                Assert(Data(elastic) == Data(changed["Elastico_SLV_H.txt"]));
                Assert(Data(files["Progetto_SLV_H.txt"]) != Data(changed["Progetto_SLV_H.txt"]));
            } finally { CultureInfo.CurrentCulture = culture; }
        });
        test("mappa: andata e ritorno WGS84/WebMercator senza scambio latitudine-longitudine", () => {
            foreach (var p in new[] { new GeographicPoint(12.19944,44.41778), new(9.19,45.46), new(15.1,37.5) })
                foreach (int z in new[] { 3,7,12,18 }) {
                    var copy = MapProjection.Unproject(MapProjection.Project(p,z),z);
                    Near(copy.Longitude,p.Longitude); Near(copy.Latitude,p.Latitude);
                }
        });
        test("calcestruzzo: formule e correzione Rck delle classi elevate", () => {
            var m = new ConcreteWorkspace();
            foreach (string cls in Enum.GetNames<Calcestruzzo.ClassiNormale>()) {
                var d = Example(m); d.Fields["classe"] = cls; var r = m.Calculate(d);
                double rck = double.Parse(cls.Split('_')[1], CultureInfo.InvariantCulture);
                Near(r[0].Valore,rck); Near(r[1].Valore,.83*rck); Near(r[2].Valore,.85*.83*rck/1.5);
                double fcm = .83*rck+8;
                Near(r[4].Valore, rck > 60 ? 2.12*Math.Log(1+fcm/10) : .3*Math.Pow(.83*rck,2d/3));
                Assert(r.All(v => !string.IsNullOrWhiteSpace(v.Formula) && !string.IsNullOrWhiteSpace(v.Sostituzione)));
            }
        });
    }
}
