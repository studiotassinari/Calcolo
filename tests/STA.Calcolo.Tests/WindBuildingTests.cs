using STA.Calcolo.Application;
using Vento.Aerodinamica;
using CP = Vento.Aerodinamica.CoefficientiPressione;

/// <summary>
/// Vento sulle costruzioni: i valori attesi sono letti direttamente da tabelle e figure della
/// Circolare 21/01/2019 n. 7, § C3.3.8, e calcolati a mano, senza passare dal codice sotto prova.
/// </summary>
internal static class WindBuildingTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception("Vento costruzioni: " + message); }
    static void Near(double actual, double expected, string name, double tolerance = 1e-9)
    {
        Assert(double.IsFinite(actual) && Math.Abs(actual - expected) < tolerance, $"{name} = {actual}, atteso {expected}");
    }
    static GeometriaCostruzione Geometry(TipologiaCostruzione type, double b = 20, double d = 12, double h = 8, double alfa = 15) =>
        new() { Tipologia = type, B = b, D = d, H = h, Alfa = alfa, Freccia = 4, NumeroTratti = 4 };
    static double Value(RisultatoCoefficienti r, string surface, string condition = "-") =>
        r.Coefficienti.Single(c => c.Superficie.StartsWith(surface) && c.Condizione == condition).Valore;

    public static void Run(Action<string, Action> test)
    {
        test("vento costruzioni: pareti, Tab. C3.3.I", () => {
            Near(CP.PareteSopravento(.5), .75, "sopravento h/d=0,5"); Near(CP.PareteSopravento(2), .8, "sopravento h/d=2");
            Near(CP.PareteLaterale(.25), -.7, "laterale h/d=0,25"); Near(CP.PareteLaterale(1), -.9, "laterale h/d=1");
            Near(CP.PareteSottovento(1), -.5, "sottovento h/d=1"); Near(CP.PareteSottovento(5), -.7, "sottovento h/d=5");
        });
        test("vento costruzioni: falda singola, Tab. C3.3.V e VI", () => {
            // estremi dei tratti della spezzata: ai nodi le due formule adiacenti devono coincidere
            Near(CP.FaldaSingolaNegativo(-60)!.Value, -.5, "neg -60"); Near(CP.FaldaSingolaNegativo(-15)!.Value, -1, "neg -15");
            Near(CP.FaldaSingolaNegativo(0)!.Value, -.8, "neg 0"); Near(CP.FaldaSingolaNegativo(30)!.Value, -.4, "neg 30");
            Near(CP.FaldaSingolaNegativo(45)!.Value, 0, "neg 45"); Assert(CP.FaldaSingolaNegativo(46) == null, "oltre 45° solo valore positivo");
            Near(CP.FaldaSingolaPositivo(45)!.Value, .6, "pos 45"); Near(CP.FaldaSingolaPositivo(75)!.Value, .8, "pos 75");
            Assert(CP.FaldaSingolaPositivo(-1) == null, "inclinazione negativa: solo valore negativo");
            Near(CP.FaldaSingolaParallelaFasciaSopravento(15), -1.1, "∥ A 15"); Near(CP.FaldaSingolaParallelaFasciaSopravento(0), -.8, "∥ A 0");
            Near(CP.FaldaSingolaParallelaRestantiZone(15), -.7, "∥ B 15"); Near(CP.FaldaSingolaParallelaRestantiZone(45), -.9, "∥ B 45");
            Near(CP.FaldaSingolaParallelaRestantiZone(75), -.5, "∥ B 75");
        });
        test("vento costruzioni: falda doppia e padiglione, Tab. C3.3.IX, X e XIII", () => {
            Near(CP.FaldaDoppiaSottovento(-75), -.85 - 15d / 180, "sottovento -75"); Near(CP.FaldaDoppiaSottovento(-15), -.6, "sottovento -15");
            Near(CP.FaldaDoppiaSottovento(45), -.3, "sottovento 45");
            Near(CP.FaldaDoppiaParallelaFasciaSopravento(0), -.8, "∥ A 0"); Near(CP.FaldaDoppiaParallelaFasciaSopravento(30), -1, "∥ A 30");
            Near(CP.FaldaDoppiaParallelaFasciaSopravento(-30), -1, "∥ A -30");
            Near(CP.FaldaDoppiaParallelaRestantiZone(-30), -.9, "∥ B -30"); Near(CP.FaldaDoppiaParallelaRestantiZone(0), -.6, "∥ B 0");
            Near(CP.FaldaDoppiaParallelaRestantiZone(10), -.5, "∥ B 10");
            Near(CP.PadiglioneFaldeLaterali(15), -.8, "padiglione 15"); Near(CP.PadiglioneFaldeLaterali(30), -1, "padiglione 30");
            Near(CP.PadiglioneFaldeLaterali(60), -.6, "padiglione 60");
        });
        test("vento costruzioni: volta cilindrica, Fig. C3.3.19 (c)", () => {
            Near(CP.VoltaZonaA(0, .25)[0], .4, "A h/d=0"); Near(CP.VoltaZonaA(1, .15)[0], -1.2, "A pianerottolo");
            var doppio = CP.VoltaZonaA(1, .3); Assert(doppio.Length == 2, "fra 0,2 e 0,3 due valori");
            Near(doppio[0], -.3, "A ramo negativo a 0,3"); Near(doppio[1], .8 / 3, "A ramo positivo a 0,3");
            Near(CP.VoltaZonaA(1, .5)[0], .8, "A f/d=0,5");
            // h/d = 0,25: media fra la curva h/d = 0 (+0,16) e quella h/d ≥ 0,5 (−1,2) a f/d = 0,1
            Near(CP.VoltaZonaA(.25, .1)[0], (.16 - 1.2) / 2, "A interpolata su h/d");
            Near(CP.VoltaZonaB(.5), -1.2, "B"); Near(CP.VoltaZonaB(.2), -.9, "B 0,2"); Near(CP.VoltaZonaC(.3), -.4, "C");
        });
        test("vento costruzioni: tettoie, Tab. C3.3.XV, XVI e XVII", () => {
            Near(CP.TettoiaSingolaPositivo(30), 1.2, "singola +"); Near(CP.TettoiaSingolaNegativo(30, 0), -1.8, "singola − φ=0");
            Near(CP.TettoiaSingolaNegativo(0, 1), -1.4, "singola − φ=1"); Near(CP.TettoiaSingolaNegativo(0, .5), -.95, "singola − φ=0,5");
            Near(CP.TettoiaDoppiaPositivo(-20), .2 + .7 * 20 / 30, "doppia +"); Near(CP.TettoiaDoppiaNegativo(30, 0), -1.1, "doppia − 30");
            Near(CP.TettoiaDoppiaNegativo(-20, 0), -.7, "doppia − −20");
            Near(CP.TettoiaMultiplaFattoreRiduttivo(1, false), .8, "primo campo −"); Near(CP.TettoiaMultiplaFattoreRiduttivo(2, true), .9, "secondo campo +");
            Near(CP.TettoiaMultiplaFattoreRiduttivo(3, true), .7, "altri campi +");
        });
        test("vento costruzioni: pressione interna e coefficiente locale", () => {
            Assert(CP.PressioneInterna(CasoPressioneInterna.NonConsiderata, .8).Length == 0, "nessun cpi");
            Assert(CP.PressioneInterna(CasoPressioneInterna.PorositàUniforme, .8).SequenceEqual(new[] { .2, -.3 }), "porosità uniforme");
            Near(CP.PressioneInterna(CasoPressioneInterna.AperturaDominanteDoppia, .8)[0], .6, "0,75 cpe");
            Near(CP.PressioneInterna(CasoPressioneInterna.AperturaDominanteTripla, -.9)[0], -.81, "0,90 cpe");
            // [C3.3.3] a metà scala logaritmica (A = √10 m²) dà la media dei due coefficienti
            Near(CP.CoefficienteLocale(-2, -1, Math.Sqrt(10)), -1.5, "cpe,A"); Near(CP.CoefficienteLocale(-2, -1, .5), -2, "A ≤ 1");
        });
        test("vento costruzioni: composizione per tipologia e quote di riferimento", () => {
            var doppia = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaFaldaDoppia));
            Near(Value(doppia, "Parete sopravento"), .7 + .1 * 8 / 12, "parete sopravento");
            Near(Value(doppia, "Falda sopravento", "valori negativi"), -.6, "falda sopravento −");
            Near(Value(doppia, "Falda sopravento", "valori positivi"), .2, "falda sopravento +");
            Near(Value(doppia, "Falda sottovento"), -.6, "falda sottovento");
            // edificio alto: la parete sopravento ha due zone, con ze = b e ze = h
            var alto = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaPiana, b: 15, h: 30));
            Assert(alto.Coefficienti.Count(c => c.Superficie.StartsWith("Parete sopravento")) == 2, "due zone sopravento");
            Near(alto.Coefficienti[0].QuotaRiferimento, 15, "ze = b"); Near(alto.Coefficienti[1].QuotaRiferimento, 30, "ze = h");
            // inclinazione entro ±5°: copertura piana
            var quasiPiana = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaFaldaSingola, alfa: 4));
            Near(Value(quasiPiana, "Copertura, fascia sopravento"), -.8, "fascia piana"); Assert(quasiPiana.Avvertenze.Count == 1, "avvertenza ±5°");
            // shed con lato alto sopravento, α = 20°: −0,5 − 40/90 ridotto di 0,8 e 0,6
            var shed = Geometry(TipologiaCostruzione.CoperturaFaldeMultiple, d: 30, alfa: 20); shed.SchemaMultiplo = SchemaFaldeMultiple.ShedInclinazioneNegativa;
            var multiple = CalcolatoreCoefficienti.Calcola(shed); double primo = -.5 - 40d / 90;
            Near(Value(multiple, "Tratto 1"), primo, "tratto 1"); Near(Value(multiple, "Tratto 2"), .8 * primo, "tratto 2"); Near(Value(multiple, "Tratti 3-4"), .6 * primo, "tratti 3-4");
            // volta: ze = h + f/2
            var volta = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaVoltaCilindrica, d: 16, h: 6));
            Near(volta.Coefficienti.Single(c => c.Superficie.StartsWith("Volta, zone B")).QuotaRiferimento, 8, "ze volta");
            // pareti della volta: ze = h + f/2 come la copertura, h/d con la quota di imposta (6/16 = 0,375)
            Near(volta.Coefficienti[0].QuotaRiferimento, 8, "ze pareti = h + f/2"); Near(Value(volta, "Parete sopravento"), .7 + .1 * .375, "cpe sopravento volta");
            Near(Value(volta, "Pareti laterali"), -.5 - .8 * .375, "cpe laterali volta"); Near(Value(volta, "Parete sottovento"), -.3 - .2 * .375, "cpe sottovento volta");
            // dati non validi: nessun coefficiente e messaggio
            var vuoto = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaPiana, b: 0));
            Assert(!vuoto.Valido && vuoto.Coefficienti.Count == 0 && vuoto.Avvertenze.Count == 1, "geometria non valida");
        });
        test("vento costruzioni: pressioni, pressione interna e forza sulle tettoie", () => {
            // categoria IV: kr = 0,22, z0 = 0,30 m, zmin = 8 m; qr = ½ · 1,25 · 25² = 390,625 N/m²
            double log = Math.Log(8 / .3), ce = .22 * .22 * log * (7 + log), qp = 390.625 * ce;
            var sito = new DatiVentoSito(390.625, "IV", 1, 1);
            var a = CalcolatoreAzioni.Calcola(sito, Geometry(TipologiaCostruzione.CoperturaFaldaDoppia), CasoPressioneInterna.PorositàUniforme, FacciaEdificio.Sopravento);
            Near(a.PressionePiccoSommità, qp, "qp sommità"); double cpe = .7 + .1 * 8 / 12;
            var parete = a.Azioni[0]; Near(parete.Ce, ce, "ce"); Near(parete.Pressione, qp * cpe, "pe");
            Near(parete.PressioneNetta1!.Value, qp * (cpe - .2), "netta cpi +0,2"); Near(parete.PressioneNetta2!.Value, qp * (cpe + .3), "netta cpi −0,3");
            Assert(parete.Forza == null && parete.Simbolo == "cpe", "edificio: nessuna forza");
            // cd moltiplica la pressione esterna ma non quella interna
            var conCd = CalcolatoreAzioni.Calcola(sito with { Cd = .9 }, Geometry(TipologiaCostruzione.CoperturaFaldaDoppia), CasoPressioneInterna.PorositàUniforme, FacciaEdificio.Sopravento);
            Near(conCd.Azioni[0].PressioneNetta1!.Value, qp * (.9 * cpe - .2), "cd solo su pe");
            // tettoia a falda singola, α = 10°, φ = 0,5: h = 5 m è sotto zmin, quindi ce resta quello di 8 m
            var g = Geometry(TipologiaCostruzione.TettoiaFaldaSingola, h: 5, alfa: 10); g.GradoBloccaggio = .5;
            var t = CalcolatoreAzioni.Calcola(sito, g, CasoPressioneInterna.PorositàUniforme, FacciaEdificio.Sopravento);
            double area = 20 * 12 / Math.Cos(10 * Math.PI / 180), cf = (-.5 - 1.3 * 10 / 30 - 1.4) / 2;
            Assert(t.Cpi.Length == 0 && t.Azioni.All(r => r.PressioneNetta1 == null), "tettoia: nessuna pressione interna");
            Near(t.Azioni[0].Coefficiente, .2 + 10d / 30, "cF +"); Near(t.Azioni[1].Coefficiente, cf, "cF −");
            Near(t.Azioni[1].Forza!.Value, qp * cf * area / 1000, "F [kN]", 1e-6);
            // quota oltre il campo del profilo di esposizione: rifiutata, non estrapolata
            try { CalcolatoreAzioni.Calcola(sito, Geometry(TipologiaCostruzione.CoperturaPiana, h: 250), CasoPressioneInterna.NonConsiderata, FacciaEdificio.Sopravento); }
            catch (ArgumentException) { return; }
            throw new Exception("Vento costruzioni: quota oltre 200 m accettata.");
        });
        test("vento costruzioni: estensione delle zone lungo la direzione del vento", () => {
            // Volta b = 90, d = 80, h = 5, f = 10 m: R = (40² + 10²)/(2·10) = 85 m, θ = asin(40/85), L = 2·R·θ.
            double theta = Math.Asin(40d / 85), sviluppo = 2 * 85 * theta;
            Near(CalcolatoreCoefficienti.SviluppoVolta(80, 10, out double raggio, out double semiAngolo), sviluppo, "sviluppo"); Near(raggio, 85, "raggio"); Near(semiAngolo, theta, "semiangolo");
            var volta = CalcolatoreCoefficienti.Calcola(new() { Tipologia = TipologiaCostruzione.CoperturaVoltaCilindrica, B = 90, D = 80, H = 5, Freccia = 10 });
            var a = volta.Coefficienti.Single(c => c.Superficie.StartsWith("Volta, zona A")).Tratti.Single();
            var b = volta.Coefficienti.Single(c => c.Superficie.StartsWith("Volta, zone B")).Tratti.Single();
            var c2 = volta.Coefficienti.Single(c => c.Superficie.StartsWith("Volta, zona C")).Tratti.Single();
            // quattro zone di uguale sviluppo: A = L/4, B = L/2, C = L/4; il confine A/B è a x = d/2 − R·sin(θ/2)
            Near(a.Lunghezza, sviluppo / 4, "sviluppo A"); Near(b.Lunghezza, sviluppo / 2, "sviluppo B"); Near(c2.Lunghezza, sviluppo / 4, "sviluppo C");
            Near(a.XInizio, 0, "x inizio A"); Near(a.XFine, 40 - 85 * Math.Sin(theta / 2), "x fine A"); Near(b.XFine, 40 + 85 * Math.Sin(theta / 2), "x fine B"); Near(c2.XFine, 80, "x fine C");
            Near(volta.Coefficienti.Single(c => c.Superficie.StartsWith("Volta, zona A")).Valore, .2 + .125 * (-1.2 - .2), "cpe,A interpolato");
            // falda doppia a 30°: s = x / cos α
            var doppia = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaFaldaDoppia, alfa: 30));
            var sottovento = doppia.Coefficienti.Single(c => c.Superficie == "Falda sottovento").Tratti.Single();
            Near(sottovento.XInizio, 6, "x sottovento"); Near(sottovento.SFine, 12 / Math.Cos(Math.PI / 6), "s sottovento");
            // copertura piana: fascia sopravento min(b/2; h) = 8 m, poi le restanti zone fino a d
            var piana = CalcolatoreCoefficienti.Calcola(Geometry(TipologiaCostruzione.CoperturaPiana));
            Near(piana.Coefficienti.Single(c => c.Superficie.StartsWith("Copertura, fascia")).Tratti.Single().XFine, 8, "fascia"); Near(piana.Coefficienti.Last().Tratti.Single().XInizio, 8, "restanti zone");
            // falde con colmi, n = 2: prima falda, un impluvio e la falda discendente finale; le mezze falde coprono tutta d senza sovrapporsi
            var colmi = Geometry(TipologiaCostruzione.CoperturaFaldeMultiple, d: 24, alfa: 20); colmi.NumeroTratti = 2; colmi.SchemaMultiplo = SchemaFaldeMultiple.FaldeDoppieConColmi;
            var multiple = CalcolatoreCoefficienti.Calcola(colmi).Coefficienti.Where(c => c.Parte == ParteCostruzione.Copertura && c.Condizione != "valori positivi").ToList();
            Near(multiple.SelectMany(c => c.Tratti).Sum(t => t.XFine - t.XInizio), 24, "copertura completa di d");
            Assert(multiple.Any(c => c.Superficie == "Tratto 3, falda discendente") && !multiple.Any(c => c.Superficie == "Tratto 3, falda ascendente"), "ultima falda discendente");
            // pareti e falde laterali non hanno estensione lungo il vento
            Assert(doppia.Coefficienti.Where(c => c.Parte != ParteCostruzione.Copertura).All(c => c.Tratti.Count == 0), "pareti senza tratti");
        });
        test("vento costruzioni: valutazioni h/d e f/d della volta", () => {
            var azioni = CalcolatoreAzioni.Calcola(new(390.625, "III", 1, 1), new() { Tipologia = TipologiaCostruzione.CoperturaVoltaCilindrica, B = 90, D = 80, H = 5, Freccia = 10 },
                CasoPressioneInterna.NonConsiderata, FacciaEdificio.Sopravento);
            string Valore(string nome) => azioni.Valutazioni.Single(v => v.Grandezza == nome).Valore;
            Assert(Valore("h, quota di imposta") == "5 m" && Valore("d, corda della volta") == "80 m" && Valore("f, freccia") == "10 m", "dati h, d, f");
            Assert(Valore("h/d") == "0,063" && Valore("f/d") == "0,125", "rapporti h/d e f/d");
            Assert(Valore("Quota di riferimento della copertura") == "10 m" && Valore("Peso dell'interpolazione su h/d") == "0,125", "ze e peso");
            // pareti: ze = h + f/2 = 10 m, h/d = 5/80; categoria III: ce(10) = 0,2²·ln(100)·[7 + ln(100)]
            Assert(Valore("Quota di riferimento delle pareti") == "10 m" && Valore("h/d per le pareti") == "0,063", "valutazioni delle pareti");
            double ce10 = .04 * Math.Log(100) * (7 + Math.Log(100)); var parete = azioni.Azioni[0];
            Near(parete.Ze, 10, "ze parete"); Near(parete.Coefficiente, .7 + .1 * .0625, "cpe parete"); Near(parete.Pressione, 390.625 * ce10 * (.7 + .1 * .0625), "pressione parete", 1e-9);
            Near(azioni.Azioni[1].Coefficiente, -.5 - .8 * .0625, "cpe laterali"); Near(azioni.Azioni[2].Coefficiente, -.3 - .2 * .0625, "cpe sottovento");
            Assert(azioni.Valutazioni.Single(v => v.Grandezza == "f/d").Nota.StartsWith("0,05 < f/d < 0,2") && azioni.Valutazioni.Single(v => v.Grandezza == "h/d").Nota.Contains("interpolazione"), "campo applicato");
            Assert(azioni.Condizioni.Count == 0 && azioni.PerCondizione("").Count == azioni.Azioni.Count, "una sola condizione di carico");
            Assert(azioni.Azioni.Single(r => r.Superficie.StartsWith("Volta, zona A")).Estensione.StartsWith("x = "), "estensione leggibile");
        });
        test("vento costruzioni: progetto JSON, andata e ritorno e file non validi", () => {
            var sito = WindBuildingProject.SiteFields.ToDictionary(f => f.Key, f => f.Default); sito["quota"] = "350";
            var geometria = Geometry(TipologiaCostruzione.CoperturaFaldeMultiple, alfa: -12.5); geometria.SchemaMultiplo = SchemaFaldeMultiple.ShedInclinazionePositiva; geometria.GradoBloccaggio = .25;
            var dati = new WindBuildingInput(sito, geometria, CasoPressioneInterna.AperturaDominanteDoppia, FacciaEdificio.Sottovento);
            var documento = ModuleFiles.Read<ModuleDocument>(ModuleFiles.Write(WindBuildingProject.Write(dati)));
            var letto = WindBuildingProject.Read(documento);
            Assert(documento.Module == "vento-costruzione" && letto.Geometry.Tipologia == geometria.Tipologia && letto.Geometry.SchemaMultiplo == geometria.SchemaMultiplo, "tipologia e schema");
            Near(letto.Geometry.Alfa, -12.5, "alfa"); Near(letto.Geometry.GradoBloccaggio, .25, "bloccaggio"); Assert(letto.Geometry.NumeroTratti == 4 && letto.Site["quota"] == "350", "tratti e sito");
            Assert(letto.InternalCase == CasoPressioneInterna.AperturaDominanteDoppia && letto.DominantFace == FacciaEdificio.Sottovento, "pressione interna");
            Near(WindBuildingProject.Calculate(letto).Actions.Azioni[0].Pressione, WindBuildingProject.Calculate(dati).Actions.Azioni[0].Pressione, "stesso risultato dopo la rilettura");
            void Rifiuta(Action<Dictionary<string, string>> modifica)
            {
                var campi = new Dictionary<string, string>(documento.Fields); modifica(campi);
                try { WindBuildingProject.Read(documento with { Fields = campi }); } catch (ArgumentException) { return; }
                throw new Exception("Vento costruzioni: progetto non valido accettato.");
            }
            Rifiuta(f => f["tipologia"] = "Inesistente"); Rifiuta(f => f.Remove("b")); Rifiuta(f => f["estraneo"] = "1"); Rifiuta(f => f["tratti"] = "0"); Rifiuta(f => f["quota"] = "5000"); Rifiuta(f => f["d"] = "abc");
            try { WindBuildingProject.Read(documento with { Module = "neve" }); } catch (ArgumentException) { return; }
            throw new Exception("Vento costruzioni: progetto di un altro modulo accettato.");
        });
    }
}
