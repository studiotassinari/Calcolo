using System.Globalization;
using STA.MuroSostegno.Core;
namespace STA.Calcolo.Reporting;

/// <summary>
/// Relazione di calcolo del muro di sostegno (motore rivisto NTC 2018): inquadramento normativo, dati,
/// formule con sostituzione numerica ("compilate"), figure esplicative e riepilogo delle verifiche.
/// Ogni pagina è dimensionata per restare entro l'area utile A4 del PdfReportWriter.
/// Le figure sono PNG prodotti dall'applicazione (chiavi: "sezione", "statica", "sisma"); se mancano la pagina resta senza figura.
/// Unità: m, kN/m, kN·m/m, kPa (per metro di sviluppo del muro).
/// </summary>
public static class RetainingWallReportBuilder
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string N(double value, int digits = 2) => double.IsFinite(value) ? value.ToString("N" + digits, It) : "—";
    private static string S(double? value, int digits = 1) => value is double v ? N(v, digits) : "fuori base";
    private static string Deg(double radians) => N(radians * 180 / Math.PI, 2) + "°";
    private static string Verdict(WallCheck c) => c.Ratio is double r ? $"{N(r)} {(r >= 1 ? "≥ 1 → verificata" : "< 1 → NON verificata")}" : "azione nulla → verificata";

    public static ReportDocument Build(WallDocument document, WallDesignResult r, IReadOnlyDictionary<string, byte[]> figures, DateTime created)
    {
        WallFiles.Validate(document);
        var d = document.Parameters;
        ReportFigure[] Figure(string key, string title, string caption) =>
            figures.TryGetValue(key, out var png) && png.Length > 0 ? [new(title, png, caption)] : [];
        var so = r.StaticOverturning; var ss = r.StaticSliding; var eo = r.SeismicOverturning; var es = r.SeismicSliding;
        var sb = r.StaticBase; var eb = r.SeismicBase; var sv = r.Service;
        var b = r.Geometry.Wall[8].X;

        // ── 1. Inquadramento normativo e combinazioni.
        var norms = new ReportTable("Riferimenti normativi", ["Riferimento", "Contenuto applicato"], [
            ["D.M. 17/01/2018 (NTC 2018) §2.5.3, §2.6.1", "Combinazioni delle azioni; coefficienti parziali sulle azioni Tab. 2.6.I (EQU, A1)"],
            ["NTC 2018 §6.2.4.1, Tab. 6.2.II", "Coefficienti parziali sui parametri geotecnici (M1, M2)"],
            ["NTC 2018 §6.5.3.1.1, Tab. 6.5.I", "Muri di sostegno: ribaltamento (EQU + M2), scorrimento (Approccio 2, A1 + M1 + R3), resistenza del terreno a valle"],
            ["NTC 2018 §7.11.6.2, Tab. 7.11.III", "Muri in zona sismica: metodo pseudostatico, kv = ±0,5·kh, coefficienti γR sismici"],
            ["Circolare C.S.LL.PP. 21/01/2019 n. 7, C6.5.3 e C7.11.6", "Istruzioni applicative per le verifiche dei muri di sostegno"],
            ["UNI EN 1997-1, UNI EN 1998-5 (riferimento metodologico)", "Spinte attive e passive in condizioni statiche e sismiche, piano di spinta virtuale"]]);
        var factors = new ReportTable("Combinazioni e coefficienti parziali", ["Verifica", "Combinazione", "γG1 fav./sfav.", "γQ", "γφ′", "γR"], [
            ["Ribaltamento statico", "EQU + M2", "0,90 / 1,10", "1,50", "1,25", N(WallDesignCalculator.GammaROverturningStatic)],
            ["Scorrimento statico", "A1 + M1 + R3", "1,00 / 1,30", "1,50", "1,00", $"{N(WallDesignCalculator.GammaRSlidingStatic)} (passiva {N(WallDesignCalculator.GammaRPassiveStatic)}, quota {N(WallDesignCalculator.PassiveShare * 100, 0)} %)"],
            ["Pressione sul terreno SLU", "A1 (single source)", "1,30", "1,50", "1,00", "—"],
            ["Pressione in esercizio", "Caratteristica (SLE)", "1,00", "1,00", "1,00", "—"],
            ["Sisma (ribaltamento, scorrimento, terreno)", $"G + E + ψ·Q (ψ = {N(d["Psi_Sovr"])})", "1,00", "1,00", "1,00", $"{N(WallDesignCalculator.GammaROverturningSeismic)} / {N(WallDesignCalculator.GammaRSlidingSeismic)} (passiva {N(WallDesignCalculator.GammaRPassiveSeismic)})"]]);
        var page1 = new ReportPage("Muro di sostegno · inquadramento normativo", [norms, factors], [], [
            document.Title,
            "Metodo: equilibrio limite del blocco muro + terreno sovrastante il tallone, con spinta sul piano verticale virtuale per l'estremità del tallone (x = Bf) e resistenza passiva sul piano verticale per il piede (x = 0). Analisi sismica pseudostatica. Risultati per metro di sviluppo.",
            "Esclusi dal calcolo: capacità portante, stabilità globale, cedimenti, dimensionamento delle armature e verifiche strutturali delle sezioni."]);

        // ── 2. Geometria con figura.
        string[] Row(string key) { var p = WallParameters.All.Single(x => x.Key == key); return [p.Label, N(d[key], p.Unit == "%" ? 1 : 3) + " " + p.Unit]; }
        var geometryKeys = WallParameters.All.Where(p => p.Group == "Geometria").Select(p => p.Key);
        // Due coppie parametro/valore per riga: la tabella resta compatta e lascia spazio alla figura.
        var geometryRows = geometryKeys.Select(Row).Chunk(2).Select(pair => pair.Length == 2 ? new[] { pair[0][0], pair[0][1], pair[1][0], pair[1][1] } : [pair[0][0], pair[0][1], "", ""]).ToArray();
        var page2 = new ReportPage("Geometria della sezione", [new("Dati geometrici", ["Parametro", "Valore", "Parametro", "Valore"], geometryRows)], [],
            ["Origine nello spigolo anteriore inferiore della fondazione; x verso monte, y verso l'alto. Quote di falda assolute rispetto all'origine."],
            "192845", null, Figure("sezione", "Figura 1 · Sezione del muro", "Geometria, terreni e falde con le quote principali (scala uguale sui due assi)."));

        // ── 3. Materiali, terreno e azione sismica (dati).
        var otherKeys = WallParameters.All.Where(p => p.Group != "Geometria").Select(p => p.Key);
        var page3 = new ReportPage("Materiali, terreno e azione sismica", [new("Dati di calcolo", ["Parametro", "Valore"], otherKeys.Select(Row).ToArray())], [], [
            $"Terreno sotto falda con peso efficace γ′ = γ − γw = {N(d["GammaTerreno"])} − {N(WallDesignCalculator.GammaW)} = {N(d["GammaTerreno"] - WallDesignCalculator.GammaW)} kN/m³ (γsat ≈ γ).",
            "La coesione c′ₖ non è utilizzata (a favore di sicurezza). PGA = amax/g comprensiva dell'amplificazione stratigrafica e topografica (S·ag/g)."]);

        // ── 4. Azione sismica.
        var ratio = d["H1"] / (d["Tm"] * d["Vs"]);
        var page4 = new ReportPage("Azione sismica pseudostatica", [], [], [
            "Il tratto variabile di B_H fra 0,05 e 0,4 è reso continuo fino a 0,4: verificare la fonte normativa del coefficiente adottato nel programma originale."],
            "135F6E", [
                new("(1) Parametro di altezza", "ρH", "H1", "Vs · Tm", "Rapporto fra l'altezza del muro e la lunghezza d'onda di taglio (adimensionale).",
                    $"ρH = {N(d["H1"])} / ({N(d["Vs"], 3)} × {N(d["Tm"])}) = {N(ratio, 4)}"),
                new("(2) Coefficiente di riduzione per l'altezza", "B_H", "1 − (1 − 0,4)·(ρH − 0,05)", "0,4 − 0,05", "B_H = 1 per ρH ≤ 0,05; B_H = 0,4 per ρH ≥ 0,4; lineare nel tratto intermedio.",
                    $"B_H = {N(r.BH, 4)}"),
                new("(3) Coefficiente sismico orizzontale", "kh", "B_H · PGA", "χ", "χ coefficiente di riduzione assegnato.",
                    $"kh = {N(r.BH, 4)} × {N(d["PGA"], 3)} / {N(d["Csi_H"], 3)} = {N(r.Kh, 4)}"),
                new("(4) Coefficiente verticale e angolo sismico", "θ", "atan[ kh / (1 ± kv) ]", "", "NTC 2018 §7.11.6.2.1: kv = ±0,5·kh; si analizzano entrambi i versi e si riporta il più gravoso.",
                    $"kv = ±{N(.5 * r.Kh, 4)} → θ = {N(r.Cases[2].ThetaDegrees, 3)}° (kv verso l'alto), {N(r.Cases[3].ThetaDegrees, 3)}° (kv verso il basso)")]);

        // ── 5. Coefficienti di spinta.
        var phiK = d["FI_T"] * Math.PI / 180; var deltaK = d["Delta_T_M"] * Math.PI / 180;
        double Design(double angle, double gamma) => Math.Atan(Math.Tan(angle) / gamma);
        var beta = Math.Atan(d["Itp"] / 100); var betaFront = -Math.Atan(d["It"] / 100);
        var kTable = new ReportTable("Coefficienti per combinazione", ["Combinazione", "φ′d", "δd", "θ", "KA", "KP (δ = 0)"],
            r.Cases.Select(c => new[] { c.Name, Deg(Design(phiK, c.Factors == "EQU + M2" ? 1.25 : 1)), Deg(Design(deltaK, c.Factors == "EQU + M2" ? 1.25 : 1)), N(c.ThetaDegrees, 2) + "°", N(c.KaOverturning, 4), N(c.Kp, 4) }).ToArray());
        var page5 = new ReportPage("Coefficienti di spinta", [kTable], [], [
            "Formule (F.1)–(F.4) del programma originale, valutate con i parametri di progetto di ciascuna combinazione e con l'angolo sismico θ; il coefficiente è riferito alla componente orizzontale, la componente verticale vale tan δd volte quella orizzontale."],
            "135F6E", [
                new("(5) Coefficiente di spinta attiva (F.1)", "KA", "cos δ · [cos δ − √(sin²φ′ − sin²δ)] · cos β · e^(−2 ψA tan φ′)", "[cos(β + θ) + √(sin²φ′ − sin²(β + θ))] · cos θ",
                    "ψA = ½·[asin(sin δ / sin φ′) − asin(sin(β + θ) / sin φ′) − δ + β − θ]; β = atan(Itp/100) pendenza a monte.",
                    $"Scorrimento statico: φ′ = {Deg(phiK)}, δ = {Deg(deltaK)}, β = {Deg(beta)}, θ = 0 → KA = {N(ss.KaSliding, 4)}"),
                new("(6) Coefficiente di resistenza passiva (F.2)", "KP", "cos δ · [cos δ + √(sin²φ′ − sin²δ)] · cos β · e^(+2 ψP tan φ′)", "[cos(β − θ) − √(sin²φ′ − sin²(β − θ))] · cos θ",
                    "ψP = ½·[asin(sin δ / sin φ′) + asin(sin(β − θ) / sin φ′) + δ + β + θ]; a valle β = −atan(It/100), δ = 0 (cautelativo).",
                    $"Scorrimento statico: φ′ = {Deg(phiK)}, δ = 0, β = {Deg(betaFront)} → KP = {N(ss.Kp, 4)}"),
                new("(7) Spinta del terreno e dell'acqua", "Sa", "∫ KA · (1 ± kv) · σ′v(z) dz + ∫ KA · (1 ± kv) · q dz", "", "σ′v tensione verticale efficace (γ sopra falda, γ′ sotto falda); acqua: Sw = γw·hw²/2.",
                    $"Scorrimento statico: Sa = {N(ss.EarthThrust, 1)} kN/m, Sq = {N(ss.SurchargeThrust, 1)} kN/m, Sw = {N(ss.WaterBack, 1)} kN/m")]);

        // ── 6. Azioni statiche (figura + tabella).
        ReportTable Forces(WallDesignCase c, string title) => new(title, ["Azione", "Fx [kN/m]", "Fy [kN/m]", "x; y [m]", "Mz piede [kN·m/m]"],
            c.Forces.Select(f => new[] { f.Name, N(f.Fx, 1), N(f.Fy, 1), $"{N(f.X)}; {N(f.Y)}", N(f.Mz, 1) }).ToArray());
        var page6 = new ReportPage("Azioni sul muro · combinazione statica", [Forces(sb, $"Azioni di progetto ({sb.Factors}, permanenti ×{N(sb.GammaBaseG)}, variabili ×{N(sb.GammaBaseQ)})")], [], [
            "Fx positivo verso monte, Fy positivo verso l'alto; Mz positivo se ribaltante attorno al piede anteriore."],
            "192845", null, Figure("statica", "Figura 2 · Forze e diagrammi di pressione in condizioni statiche",
                "Frecce proporzionali alle azioni di progetto; σa spinta attiva, u pressione dell'acqua, σp resistenza passiva, σ terreno pressione di contatto sulla base."));

        // ── 7. Ribaltamento.
        var page7 = new ReportPage("Verifica a ribaltamento", [], [], [
            "Per ogni azione il coefficiente parziale è scelto in base all'effetto (favorevole se stabilizzante). La resistenza passiva a valle non è considerata."],
            "135F6E", [
                new("(8) Momento ribaltante di progetto (statica, EQU + M2)", "Mrib,d", "Σ γF · Mz,i (azioni ribaltanti)", "", "Momenti delle azioni attorno al piede anteriore.", $"Mrib,d = {N(so.OverturningMoment, 1)} kN·m/m"),
                new("(9) Momento stabilizzante di progetto (statica)", "Mstab,d", "Σ γF · |Mz,i| (azioni stabilizzanti)", "", "Pesi propri, componente verticale della spinta, acqua a valle.", $"Mstab,d = {N(so.StabilizingMoment, 1)} kN·m/m"),
                new("(10) Verifica statica", "Rd / Ed", "Mstab,d", "γR · Mrib,d", "NTC 2018 Tab. 6.5.I: γR = 1,15.",
                    $"{N(so.StabilizingMoment, 1)} / ({N(so.Overturning.GammaR)} × {N(so.OverturningMoment, 1)}) = {Verdict(so.Overturning)}"),
                new("(11) Verifica sismica (" + eo.Name + ")", "Rd / Ed", "Mstab,d", "γR · Mrib,d", "NTC 2018 Tab. 7.11.III: γR = 1,0; azioni con γ = 1, inerzie kh·W.",
                    $"{N(eo.StabilizingMoment, 1)} / ({N(eo.Overturning.GammaR)} × {N(eo.OverturningMoment, 1)}) = {Verdict(eo.Overturning)}")]);

        // ── 8. Scorrimento.
        double Rd(WallDesignCase c) => c.Sliding.Resistance / c.Sliding.GammaR;
        var tanPhi = Math.Tan(phiK);
        var page8 = new ReportPage("Verifica a scorrimento", [], [], [
            "Piano di scorrimento orizzontale per la base del dente; il blocco comprende il terreno fra la base e il piano. Il peso del sovraccarico non è portato in conto."],
            "135F6E", [
                new("(12) Azione orizzontale di progetto (statica, A1)", "Ed", "γG1 · (Sa + Sw) + γQ · Sq − γG1,fav · Sw,v", "", "Spinte sul piano verticale per la base del dente.",
                    $"Ed = 1,30 × ({N(ss.EarthThrust, 1)} + {N(ss.WaterBack, 1)}) + 1,50 × {N(ss.SurchargeThrust, 1)} − 1,00 × {N(ss.WaterFront, 1)} = {N(ss.Driving, 1)} kN/m"),
                new("(13) Azione normale di progetto", "N", "γG1,fav · ΣW + tan δd · (γG1 · Sa + γQ · Sq) − γG1 · U", "", "U sottospinta sul piano di scorrimento.",
                    $"N = {N(ss.Normal, 1)} kN/m (U = {N(ss.Uplift, 1)} kN/m)"),
                new("(14) Resistenza di progetto", "Rd", "N · tan φ′d", "γR", $"Più il {N(WallDesignCalculator.PassiveShare * 100, 0)} % della resistenza passiva a valle Sp divisa per γR,valle.",
                    $"Rd = {N(ss.Normal, 1)} × {N(tanPhi, 3)} / {N(ss.Sliding.GammaR)} + {N(WallDesignCalculator.PassiveShare)} × {N(ss.Passive, 1)} / {N(WallDesignCalculator.GammaRPassiveStatic)} = {N(Rd(ss), 1)} kN/m"),
                new("(15) Verifica statica", "Rd / Ed", "Rd", "Ed", "Verificata se il rapporto è almeno 1.", $"{N(Rd(ss), 1)} / {N(ss.Driving, 1)} = {Verdict(ss.Sliding)}"),
                new("(16) Verifica sismica (" + es.Name + ")", "Rd / Ed", "N · tan φ′ / γR + 0,5 · Sp / γR,valle", "Ed", "Ed comprende le forze d'inerzia del blocco.",
                    $"({N(es.Normal, 1)} × {N(tanPhi, 3)} / {N(es.Sliding.GammaR)} + 0,5 × {N(es.Passive, 1)} / {N(WallDesignCalculator.GammaRPassiveSeismic)}) / {N(es.Driving, 1)} = {Verdict(es.Sliding)}")]);

        // ── 9. Pressione sul terreno di fondazione.
        string Sigma(WallBasePressure p) => p.SigmaToe is null ? "risultante fuori dalla base"
            : p.InCore ? $"σ = {N(p.N, 1)} / {N(p.B)} × (1 ± 6 × {N(p.Eccentricity, 3)} / {N(p.B)}) → valle {S(p.SigmaToe)} kPa, monte {S(p.SigmaHeel)} kPa"
            : $"base parzializzata: σmax = 2 × {N(p.N, 1)} / (3 × {N(p.Eccentricity > 0 ? p.U : p.B - p.U, 3)}) = {S(p.SigmaMax)} kPa su {N(p.ContactLength)} m";
        var pressureTable = new ReportTable("Pressioni di contatto", ["Combinazione", "N [kN/m]", "u [m]", "e [m]", "σ valle [kPa]", "σ monte [kPa]"],
            new[] { sv, sb, eb }.Select(c => { var p = c.Diagrams.Base; return new[] { c.Name, N(p.N, 1), N(p.U), N(p.Eccentricity, 3), S(p.SigmaToe), S(p.SigmaHeel) }; }).ToArray());
        var page9 = new ReportPage("Pressione sul terreno di fondazione", [pressureTable], [], [
            $"Distribuzione lineare, terreno non reagente a trazione; base di larghezza B = Bf = {N(b)} m, nocciolo centrale |e| ≤ B/6 = {N(b / 6, 3)} m. La capacità portante non è verificata: confrontare le pressioni con la resistenza del terreno di fondazione."],
            "135F6E", [
                new("(17) Posizione della risultante dal piede", "u", "−Σ Mz,i", "N", "Momenti delle azioni attorno al piede; N risultante verticale al netto della sottospinta.",
                    $"SLU (A1): u = {N(-sb.Forces.Sum(f => f.Mz), 1)} / {N(sb.Diagrams.Base.N, 1)} = {N(sb.Diagrams.Base.U, 3)} m"),
                new("(18) Eccentricità", "e", "B / 2 − u", "", "Positiva verso valle.", $"SLU (A1): e = {N(b / 2, 3)} − {N(sb.Diagrams.Base.U, 3)} = {N(sb.Diagrams.Base.Eccentricity, 3)} m"),
                new("(19) Pressione di contatto (Navier)", "σ", "N", "B", "· (1 ± 6e/B) se |e| ≤ B/6, altrimenti σmax = 2N/(3u) su 3u. Valori SLE e sisma in tabella.", "SLU (A1): " + Sigma(sb.Diagrams.Base))]);

        // ── 10. Azioni sismiche.
        var page10 = new ReportPage("Azioni sul muro · combinazione sismica", [Forces(eb, $"Azioni sismiche ({eb.Name}, kh = {N(eb.Kh, 4)}, kv = {N(eb.Kv, 4)})")], [], [
            "Inerzie orizzontali kh·W applicate nel baricentro delle masse; spinta sismica con θ = atan[kh/(1 ± kv)]."],
            "192845", null, Figure("sisma", "Figura 3 · Forze e diagrammi di pressione in condizioni sismiche", "Combinazione sismica più gravosa per la pressione sul terreno."));

        // ── 11. Riepilogo.
        var summary = new ReportTable("Riepilogo delle verifiche", ["Verifica", "Combinazione", "Rd/Ed", "Esito"], [
            ["Ribaltamento statico", so.Factors, N(so.Overturning.Ratio ?? double.NaN), so.Overturning.Satisfied ? "Verificata" : "NON verificata"],
            ["Ribaltamento sismico", eo.Name, N(eo.Overturning.Ratio ?? double.NaN), eo.Overturning.Satisfied ? "Verificata" : "NON verificata"],
            ["Scorrimento statico", ss.Factors, N(ss.Sliding.Ratio ?? double.NaN), ss.Sliding.Satisfied ? "Verificata" : "NON verificata"],
            ["Scorrimento sismico", es.Name, N(es.Sliding.Ratio ?? double.NaN), es.Sliding.Satisfied ? "Verificata" : "NON verificata"],
            ["Pressione sul terreno SLU / sisma", "A1 · " + eb.Name, $"{S(sb.Diagrams.Base.SigmaMax)} / {S(eb.Diagrams.Base.SigmaMax)} kPa", "Da confrontare con la capacità portante"]]);
        var page11 = new ReportPage("Riepilogo e limiti del calcolo", [summary], [], r.Notes.Skip(1).Append("I risultati corrispondono agli input riportati; le modifiche al documento Word non ricalcolano il progetto.").ToArray(), "135F6E");

        return new(document.Title, created, [page1, page2, page3, page4, page5, page6, page7, page8, page9, page10, page11]);
    }
}
