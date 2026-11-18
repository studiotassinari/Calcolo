using STA.MuroSostegno.Core.Legacy;
namespace STA.MuroSostegno.Core;

// ─────────────────────────────────────────────────────────────────────────────
// Motore RIVISTO del muro di sostegno (NTC 2018, metodo pseudostatico).
// Il motore originale (WallCalculator + Legacy) resta invariato per il confronto;
// qui sono corrette le anomalie individuate nell'analisi del sorgente:
//  1. stessa altezza di spinta per ribaltamento e scorrimento;
//  2. pressioni dell'acqua idrostatiche (γw·h²/2) a monte e a valle, sottospinta sulla base;
//  3. il sovraccarico è solo un'azione, mai una resistenza;
//  4. KP calcolato con la pendenza del terreno a valle (It);
//  5. nessun poligono "immerso": l'acqua entra come pressioni sul contorno;
//  6. αH senza il fattore g spurio e B_H continuo;
//  7. forze d'inerzia del muro e del terreno, sisma verticale kv = ±0,5·kh;
//  8. coefficienti parziali NTC 2018: EQU+M2 (ribaltamento), A1+M1+R3 (scorrimento), sisma γ=1 con M1.
// Unità: m, kN, kPa, kN/m per metro di sviluppo. Sistema di riferimento di WallGeometry:
// origine nello spigolo anteriore inferiore della fondazione, x verso monte, y verso l'alto.
// Momenti attorno all'origine (piede anteriore), positivi se ribaltanti (antiorari).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Coefficienti parziali sulle azioni e sul parametro tanφ′ di una combinazione.</summary>
public sealed record WallFactors(string Name, double G1Fav, double G1Sfav, double QFav, double QSfav, double GammaPhi)
{
    /// <summary>NTC 2018 Tab. 2.6.I (EQU) con parametri M2 (Tab. 6.2.II): ribaltamento statico.</summary>
    public static WallFactors Equ { get; } = new("EQU + M2", .9, 1.1, 0, 1.5, 1.25);
    /// <summary>Approccio 2 (A1 + M1 + R3): scorrimento statico.</summary>
    public static WallFactors A1M1 { get; } = new("A1 + M1 + R3", 1, 1.3, 0, 1.5, 1);
    /// <summary>Sisma (§7.11.6): azioni con γ = 1, parametri M1; il sovraccarico è già ridotto da ψ.</summary>
    public static WallFactors Seismic { get; } = new("Sisma, γ = 1, M1", 1, 1, 0, 1, 1);
    /// <summary>Combinazione caratteristica (rara) di esercizio: tutti i coefficienti unitari; usata per le pressioni sul terreno.</summary>
    public static WallFactors Characteristic { get; } = new("Caratteristica (SLE)", 1, 1, 0, 1, 1);
}

/// <summary>
/// Azione risultante applicata al blocco muro + terreni sovrastanti (valori di progetto della combinazione,
/// con i coefficienti usati per la pressione sul terreno). Fx positivo verso monte (+x), Fy positivo verso l'alto,
/// punto di applicazione (X; Y) in metri. Kind: "G" permanenti e acqua, "Q" variabili, "E" inerzie sismiche.
/// </summary>
public sealed record WallForce(string Name, string Kind, double Fx, double Fy, double X, double Y)
{
    /// <summary>Momento attorno al piede anteriore (origine), positivo se ribaltante (antiorario) [kN·m/m].</summary>
    public double Mz => X * Fy - Y * Fx;
    public double Magnitude => Math.Sqrt(Fx * Fx + Fy * Fy);
}

/// <summary>Punto di un diagramma di pressione lungo la base: ascissa in m, pressione in kPa.</summary>
public sealed record WallBasePoint(double X, double Value);

/// <summary>
/// Pressione di contatto sul terreno di fondazione (distribuzione lineare, terreno non reagente a trazione).
/// N risultante verticale (sottospinta già detratta), U distanza della risultante dal piede, e eccentricità
/// rispetto al centro della base (positiva verso valle), B larghezza della base, ContactLength lunghezza reagente.
/// SigmaToe/SigmaHeel null se la risultante cade fuori dalla base o N ≤ 0.
/// </summary>
public sealed record WallBasePressure(double N, double U, double Eccentricity, double B, double ContactLength,
    double? SigmaToe, double? SigmaHeel, IReadOnlyList<WallBasePoint> Points)
{
    public bool InCore => SigmaToe is not null && Math.Abs(Eccentricity) <= B / 6 + 1e-12;
    public double? SigmaMax => SigmaToe is double a && SigmaHeel is double b ? Math.Max(a, b) : null;
}

/// <summary>Diagrammi di una combinazione: pressioni orizzontali a monte (terreno + sovraccarico) e dell'acqua sul piano x = Bf,
/// resistenza passiva e acqua a valle sul piano x = 0 (quote in m, kPa), pressione di contatto sulla base.</summary>
public sealed record WallDiagrams(IReadOnlyList<WallPressurePoint> Active, IReadOnlyList<WallPressurePoint> WaterBack,
    IReadOnlyList<WallPressurePoint> Passive, IReadOnlyList<WallPressurePoint> WaterFront, WallBasePressure Base);

/// <summary>Verifica Ed ≤ Rd/γR. Ratio = Rd/(γR·Ed): verificata se ≥ 1; null se l'azione è nulla o favorevole.</summary>
public sealed record WallCheck(double Demand, double Resistance, double GammaR)
{
    public double? Ratio => Demand > 1e-9 ? Resistance / (GammaR * Demand) : null;
    public bool Satisfied => Ratio is not double r || r >= 1;
}

/// <summary>Risultato di una combinazione. Forze in kN/m (orizzontali positive verso valle), momenti in kN·m/m.</summary>
public sealed record WallDesignCase(string Name, string Factors, double Kh, double Kv, double ThetaDegrees,
    double KaOverturning, double KaSliding, double Kp, double EarthThrust, double SurchargeThrust, double WaterBack,
    double WaterFront, double Uplift, double Inertia, double Normal, double Passive, double Driving,
    double StabilizingMoment, double OverturningMoment, WallCheck Overturning, WallCheck Sliding,
    IReadOnlyList<WallForce> Forces, WallDiagrams Diagrams, double GammaBaseG, double GammaBaseQ);

public sealed record WallDesignResult(WallGeometry Geometry, PolygonProperties Wall, PolygonProperties FrontSoil,
    PolygonProperties BackSoil, double Kh, double BH, IReadOnlyList<WallDesignCase> Cases,
    WallDesignCase StaticOverturning, WallDesignCase StaticSliding, WallDesignCase SeismicOverturning, WallDesignCase SeismicSliding,
    IReadOnlyList<WallPressurePoint> Pressures, IReadOnlyList<string> Notes,
    WallDesignCase Service, WallDesignCase StaticBase, WallDesignCase SeismicBase)
{
    public bool AllSatisfied => StaticOverturning.Overturning.Satisfied && StaticSliding.Sliding.Satisfied
        && SeismicOverturning.Overturning.Satisfied && SeismicSliding.Sliding.Satisfied;
}

public static class WallDesignCalculator
{
    /// <summary>Peso dell'unità di volume dell'acqua [kN/m³].</summary>
    public const double GammaW = 9.81;
    /// <summary>γR NTC 2018 Tab. 6.5.I (statica) e Tab. 7.11.III (sisma).</summary>
    public const double GammaRSlidingStatic = 1.1, GammaROverturningStatic = 1.15, GammaRPassiveStatic = 1.4;
    public const double GammaRSlidingSeismic = 1.0, GammaROverturningSeismic = 1.0, GammaRPassiveSeismic = 1.2;
    /// <summary>Quota della resistenza passiva a valle portata in conto nello scorrimento (§6.5.3.1.1: al massimo il 50 %).</summary>
    public const double PassiveShare = .5;

    public const string Scope = "Motore rivisto NTC 2018 (pseudostatico): ribaltamento EQU+M2, scorrimento A1+M1+R3, sisma con γ = 1, M1 e kv = ±0,5·kh. Esclusi capacità portante, stabilità globale, cedimenti e armature.";

    public static WallDesignResult Calculate(IReadOnlyDictionary<string,double> d)
    {
        var g = WallGeometry.Create(d);
        var wall = WallGeometry.Properties(g.Wall); var front = WallGeometry.Properties(g.FrontSoil); var back = WallGeometry.Properties(g.BackSoil);
        var (kh, bh) = SeismicCoefficient(d);
        var kv = .5 * kh;
        var q = d["Sovraccarico"]; var qSeismic = q * d["Psi_Sovr"];
        WallDesignCase[] cases = [
            Evaluate(d, g, "Statica · ribaltamento", WallFactors.Equ, 0, 0, q, GammaROverturningStatic, GammaRSlidingStatic, GammaRPassiveStatic),
            Evaluate(d, g, "Statica · scorrimento", WallFactors.A1M1, 0, 0, q, GammaROverturningStatic, GammaRSlidingStatic, GammaRPassiveStatic),
            Evaluate(d, g, "Sisma · kv verso l'alto", WallFactors.Seismic, kh, kv, qSeismic, GammaROverturningSeismic, GammaRSlidingSeismic, GammaRPassiveSeismic),
            Evaluate(d, g, "Sisma · kv verso il basso", WallFactors.Seismic, kh, -kv, qSeismic, GammaROverturningSeismic, GammaRSlidingSeismic, GammaRPassiveSeismic),
            Evaluate(d, g, "Esercizio · caratteristica", WallFactors.Characteristic, 0, 0, q, GammaROverturningStatic, GammaRSlidingStatic, GammaRPassiveStatic)];
        // Fra i due segni di kv si riporta la combinazione più gravosa per ciascuna verifica.
        static double Worst(WallCheck c) => c.Ratio ?? double.MaxValue;
        var seismicOverturning = cases[2..4].MinBy(c => Worst(c.Overturning))!;
        var seismicSliding = cases[2..4].MinBy(c => Worst(c.Sliding))!;
        // Pressione sul terreno sismica: la combinazione con la pressione massima (o con la risultante fuori base).
        var seismicBase = cases[2..4].MaxBy(c => c.Diagrams.Base.SigmaMax ?? double.MaxValue)!;
        return new(g, wall, front, back, kh, bh, cases, cases[0], cases[1], seismicOverturning, seismicSliding, Pressures(d, g), Notes(d, kh),
            cases[4], cases[1], seismicBase);
    }

    /// <summary>
    /// Coefficiente sismico orizzontale αH = B_H·PGA/χ, con B_H funzione di H/(Vs·Tm).
    /// Correzioni rispetto al sorgente: H/(Vs·Tm) senza il fattore √g (G/ρ = Vs²) e B_H continuo:
    /// 1 fino a 0,05, lineare fino a 0,4 per H/(Vs·Tm) ≥ 0,4 (nel sorgente la retta arrivava a 0,35).
    /// </summary>
    public static (double Kh, double BH) SeismicCoefficient(IReadOnlyDictionary<string,double> d)
    {
        var ratio = d["H1"] / (d["Tm"] * d["Vs"]);
        var bh = ratio <= .05 ? 1 : ratio >= .4 ? .4 : 1 - (1 - .4) / (.4 - .05) * (ratio - .05);
        return (bh * d["PGA"] / d["Csi_H"], bh);
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180;

    /// <summary>KA e KP con le formule (F.1)–(F.4) del sorgente, valutate con θ = atan(kh/(1 ∓ kv)).</summary>
    private static double Ka(double phi, double delta, double beta, double theta)
    {
        try { return SpinteDelTerreno.CalculateKAEy(phi, delta, beta, theta); }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException)
        { throw new ArgumentException("Spinta attiva non definita: la pendenza a monte più l'angolo sismico θ supera φ′ di progetto. Ridurre Itp o PGA.", ex); }
    }
    private static double Kp(double phi, double delta, double beta, double theta)
    {
        try { return SpinteDelTerreno.CalculateKPEy(phi, delta, beta, theta); }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException)
        { throw new ArgumentException("Resistenza passiva non definita: |pendenza a valle − θ| supera φ′. Ridurre It o PGA.", ex); }
    }

    /// <summary>Integrale con Simpson composito su sotto-intervalli separati nei punti di discontinuità della pendenza.</summary>
    private static double Integrate(Func<double,double> f, double a, double b, params double[] breaks)
    {
        if (b <= a) return 0;
        var points = breaks.Where(x => x > a && x < b).Append(a).Append(b).Distinct().Order().ToArray();
        double sum = 0;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            const int n = 32; var h = (points[i + 1] - points[i]) / n; double s = f(points[i]) + f(points[i + 1]);
            for (var k = 1; k < n; k++) s += (k % 2 == 1 ? 4 : 2) * f(points[i] + k * h);
            sum += s * h / 3;
        }
        return sum;
    }

    /// <summary>Tensione verticale efficace [kPa] alla quota y sotto una superficie a quota top con falda a quota water.</summary>
    private static double SigmaV(double y, double top, double water, double gamma)
    {
        if (y >= top) return 0;
        var w = Math.Min(water, top);
        return y >= w ? gamma * (top - y) : gamma * (top - w) + (gamma - GammaW) * (w - y);
    }
    private static double Pore(double y, double water) => y < water ? GammaW * (water - y) : 0;

    /// <summary>Somma con segno separata in momenti ribaltanti (positivi) e stabilizzanti (negativi).</summary>
    private sealed class Moments
    {
        public double Overturning, Stabilizing;
        public void Add(double mz) { if (mz > 0) Overturning += mz; else Stabilizing -= mz; }
    }

    private static WallDesignCase Evaluate(IReadOnlyDictionary<string,double> d, WallGeometry g, string name, WallFactors f,
        double kh, double kvSigned, double q, double gammaROverturning, double gammaRSliding, double gammaRPassive)
    {
        // Fattore sulle forze di gravità: kv positivo = accelerazione verso l'alto del terreno → peso apparente ridotto.
        var gravity = 1 - kvSigned;
        if (gravity <= 0) throw new ArgumentException("Il coefficiente sismico verticale kv = 0,5·kh deve essere minore di 1: ridurre PGA.");
        var theta = Math.Atan(kh / gravity);
        var phi = Math.Atan(Math.Tan(Rad(d["FI_T"])) / f.GammaPhi);
        var deltaK = Rad(d["Delta_T_M"]); var delta = Math.Atan(Math.Tan(deltaK) / f.GammaPhi);
        var betaBack = Math.Atan(d["Itp"] / 100);
        // Correzione 4: a valle il terreno scende allontanandosi dal muro (It > 0) → pendenza negativa per la passiva;
        // δ = 0 sul piano verticale passivo (scelta cautelativa).
        var betaFront = -Math.Atan(d["It"] / 100);
        var gamma = d["GammaTerreno"]; var gammaWall = d["GammaMuro"];
        var w = g.Wall; var bf = w[8].X;
        var top = g.BackSoil[2].Y;             // superficie del terreno a monte sul piano virtuale x = Bf
        var frontTop = g.FrontSoil[3].Y;       // superficie del terreno a valle sul piano x = 0
        var waterBack = Math.Min(d["Qfp"], top); var waterFront = Math.Min(d["Qfa"], frontTop);
        var yMin = w[11].Y;                    // base del dente: piano di scorrimento
        var toothAtHeel = w[10].X >= bf - 1e-9; var toothAtToe = w[12].X <= 1e-9;
        // Correzione 1: piano posteriore di spinta fino al punto più basso del muro su x = Bf (base del dente se è sul tallone).
        var yBackOverturning = toothAtHeel ? yMin : w[9].Y;
        var yFrontOverturning = toothAtToe ? yMin : w[0].Y;
        var needsSubmerged = waterBack > yMin || waterFront > yMin;
        if (needsSubmerged && gamma <= GammaW) throw new ArgumentException("Con falda il peso del terreno γ deve superare γw = 9,81 kN/m³.");

        var ka = Ka(phi, delta, betaBack, theta);
        var kaSliding = ka; // stessa combinazione: M2 per EQU, M1 per A1 e sisma
        var kp = Kp(phi, 0, betaFront, theta);

        // Pesi propri (G1 favorevoli) e forze d'inerzia (γ = 1).
        var bodies = new List<(PolygonProperties P, double Gamma)> {
            (WallGeometry.Properties(w), gammaWall), (WallGeometry.Properties(g.FrontSoil), gamma), (WallGeometry.Properties(g.BackSoil), gamma) };
        // Il blocco di scorrimento comprende anche il terreno fra la base e il piano orizzontale per la base del dente.
        WallPoint[] under = [new(0, yMin), new(bf, yMin), w[9], w[10], w[11], w[12], w[13], w[0]];
        var underBase = WallGeometry.Properties(under);

        var overturning = new Moments(); double inertia = 0;
        foreach (var (p, gb) in bodies)
        {
            var weight = p.Area * gb;
            var mz = -p.Centroid.X * weight * gravity;            // peso verso il basso: momento stabilizzante
            overturning.Add((mz > 0 ? f.G1Sfav : f.G1Fav) * mz);
            overturning.Add(p.Centroid.Y * kh * weight);           // inerzia orizzontale verso valle
            inertia += kh * weight;
        }

        // Spinte sul piano virtuale x = Bf. Pressioni orizzontali [kPa] in funzione della quota.
        double EarthP(double y) => ka * gravity * SigmaV(y, top, waterBack, gamma);
        double SurchargeP(double y) => y <= top ? ka * gravity * q : 0; // costante fino alla superficie inclusa
        double WaterP(double y) => Pore(y, waterBack);
        (double F, double M) Resultant(Func<double,double> p, double from, double to, params double[] breaks) =>
            (Integrate(p, from, to, breaks), Integrate(y => p(y) * y, from, to, breaks));
        var tanDelta = Math.Tan(delta);
        // Il fattore di ogni azione è scelto in base al suo effetto netto sul ribaltamento (sfavorevole se ribaltante).
        void AddThrust(Moments m, (double F, double M) r, double sfav, double fav, bool vertical)
        {
            var mz = r.M - (vertical ? bf * tanDelta * r.F : 0);  // orizzontale verso valle + componente verticale in x = Bf
            m.Add((mz > 0 ? sfav : fav) * mz);
        }
        var earthO = Resultant(EarthP, yBackOverturning, top, waterBack);
        var surchargeO = Resultant(SurchargeP, yBackOverturning, top);
        var waterO = Resultant(WaterP, yBackOverturning, top, waterBack);
        AddThrust(overturning, earthO, f.G1Sfav, f.G1Fav, true);
        AddThrust(overturning, surchargeO, f.QSfav, f.QFav, true);
        AddThrust(overturning, waterO, f.G1Sfav, f.G1Fav, false);
        // Acqua a valle sul piano x = 0: spinge verso monte (momento stabilizzante se applicata sopra il piede).
        var frontWaterO = Resultant(y => Pore(y, waterFront), yFrontOverturning, frontTop, waterFront);
        overturning.Add((-frontWaterO.M > 0 ? f.G1Sfav : f.G1Fav) * -frontWaterO.M);
        // Correzione 2/5: sottospinta sul contorno di base con quota piezometrica lineare fra valle (x = 0) e monte (x = Bf).
        double Head(double x) => waterFront + (waterBack - waterFront) * x / bf;
        double Contour(double x) => x <= w[13].X ? (w[13].X <= 1e-12 ? w[13].Y : w[0].Y + (w[13].Y - w[0].Y) * x / w[13].X)
            : x <= w[10].X ? yMin : w[9].Y;
        double UpliftP(double x) => GammaW * Math.Max(0, Head(x) - Contour(x));
        var uplift = Integrate(UpliftP, 0, bf, w[13].X, w[10].X);
        var upliftM = Integrate(x => UpliftP(x) * x, 0, bf, w[13].X, w[10].X);
        overturning.Add(f.G1Sfav * upliftM);
        // Acqua sulle facce verticali del dente interne alla fondazione (spinta verso monte sulla faccia di valle e viceversa).
        if (!toothAtToe) { var r = Resultant(y => Pore(y, Head(w[12].X)), yMin, w[13].Y); overturning.Add((-r.M > 0 ? f.G1Sfav : f.G1Fav) * -r.M); }
        if (!toothAtHeel) { var r = Resultant(y => Pore(y, Head(w[11].X)), yMin, w[10].Y); overturning.Add((r.M > 0 ? f.G1Sfav : f.G1Fav) * r.M); }

        // ── Scorrimento sul piano orizzontale y = yMin (fattori per categoria: spinte e sottospinta sfavorevoli, pesi favorevoli).
        var earthS = Resultant(EarthP, yMin, top, waterBack);
        var surchargeS = Resultant(SurchargeP, yMin, top);
        var waterS = Resultant(WaterP, yMin, top, waterBack);
        var frontWaterS = Resultant(y => Pore(y, waterFront), yMin, frontTop, waterFront);
        var upliftS = GammaW * Integrate(x => Math.Max(0, Head(x) - yMin), 0, bf);
        var driving = f.G1Sfav * (earthS.F + waterS.F) + f.QSfav * surchargeS.F + inertia + kh * underBase.Area * gamma - f.G1Fav * frontWaterS.F;
        var weights = bodies.Sum(b => b.P.Area * b.Gamma) + underBase.Area * gamma;
        var normal = f.G1Fav * weights * gravity + tanDelta * (f.G1Sfav * earthS.F + f.QSfav * surchargeS.F) - f.G1Sfav * upliftS;
        var passive = Integrate(y => kp * gravity * SigmaV(y, frontTop, waterFront, gamma), yMin, frontTop, waterFront);
        // Rd = N·tanφ′d/γR + quota passiva/γR,valle, espresse come un'unica resistenza equivalente rispetto a γR dello scorrimento.
        var resistance = Math.Max(0, normal) * Math.Tan(phi) + PassiveShare * passive * gammaRSliding / gammaRPassive;

        // ── Azioni risultanti sul blocco muro + terreni (contorno reale) per pressione sul terreno, disegno e relazione.
        // Pressione sul terreno: approccio 2 (A1) con tutte le azioni permanenti ×1,3 ("single source") e variabili ×1,5;
        // combinazioni caratteristica e sismica con coefficienti unitari.
        var gBase = f == WallFactors.A1M1 ? f.G1Sfav : 1.0; var gQ = f == WallFactors.A1M1 ? f.QSfav : 1.0;
        var forces = new List<WallForce>();
        string[] names = ["Peso del muro", "Peso del terreno a valle", "Peso del terreno a monte"];
        double inertiaX = 0, inertiaY = 0;
        for (var i = 0; i < bodies.Count; i++)
        {
            var (p, gbody) = bodies[i]; var weight = p.Area * gbody;
            if (weight > 1e-9) forces.Add(new(names[i], "G", 0, -gBase * weight * gravity, p.Centroid.X, p.Centroid.Y));
            inertiaX += kh * weight * p.Centroid.X; inertiaY += kh * weight * p.Centroid.Y;
        }
        if (inertia > 1e-9) forces.Add(new("Forze d'inerzia (kh·W)", "E", -inertia, 0, inertiaX / inertia, inertiaY / inertia));
        void Thrust(string label, string kind, (double F, double M) r, double factor, double x, double sign, bool vertical)
        {
            if (r.F <= 1e-9) return;
            // sign = −1: spinta verso valle (piano a monte); +1: verso monte (piano a valle).
            forces.Add(new(label, kind, sign * factor * r.F, vertical ? -factor * r.F * tanDelta : 0, x, r.M / r.F));
        }
        Thrust("Spinta del terreno", "G", earthO, gBase, bf, -1, true);
        Thrust("Spinta del sovraccarico", "Q", surchargeO, gQ, bf, -1, true);
        Thrust("Spinta dell'acqua a monte", "G", waterO, gBase, bf, -1, false);
        Thrust("Spinta dell'acqua a valle", "G", frontWaterO, gBase, 0, 1, false);
        if (uplift > 1e-9) { var xu = upliftM / uplift; forces.Add(new("Sottospinta", "G", 0, gBase * uplift, xu, Contour(xu))); }
        if (!toothAtToe) Thrust("Acqua sul dente (valle)", "G", Resultant(y => Pore(y, Head(w[12].X)), yMin, w[13].Y), gBase, w[12].X, 1, false);
        if (!toothAtHeel) Thrust("Acqua sul dente (monte)", "G", Resultant(y => Pore(y, Head(w[11].X)), yMin, w[10].Y), gBase, w[11].X, -1, false);
        var basePressure = BasePressure(forces, bf);

        // Diagrammi (pressioni della combinazione, senza coefficienti sulle azioni): piano a monte fino al piano di scorrimento.
        double[] BackLevels(double bottom, double surface, double water) =>
            new[] { surface, water, bottom }.Where(y => y <= surface && y >= bottom).Distinct().OrderDescending().ToArray();
        var backLevels = BackLevels(yMin, top, waterBack);
        var frontLevels = BackLevels(yMin, frontTop, waterFront);
        var diagrams = new WallDiagrams(
            backLevels.Select(y => new WallPressurePoint(y, EarthP(y) + SurchargeP(y))).ToArray(),
            waterBack > yMin ? BackLevels(yMin, waterBack, waterBack).Select(y => new WallPressurePoint(y, WaterP(y))).ToArray() : [],
            frontLevels.Select(y => new WallPressurePoint(y, kp * gravity * SigmaV(y, frontTop, waterFront, gamma))).ToArray(),
            waterFront > yMin ? BackLevels(yMin, waterFront, waterFront).Select(y => new WallPressurePoint(y, Pore(y, waterFront))).ToArray() : [],
            basePressure);

        return new(name, f.Name, kh, kvSigned, theta * 180 / Math.PI, ka, kaSliding, kp, earthS.F, surchargeS.F, waterS.F, frontWaterS.F,
            upliftS, inertia, normal, passive, driving, overturning.Stabilizing, overturning.Overturning,
            new(overturning.Overturning, overturning.Stabilizing, gammaROverturning), new(driving, resistance, gammaRSliding),
            forces, diagrams, gBase, gQ);
    }

    /// <summary>
    /// Pressione di contatto con distribuzione lineare (Navier) sulla base di larghezza B = Bf, misurata dal piede.
    /// Equilibrio dei momenti attorno al piede: la reazione verticale N agisce a distanza u = −ΣMz/N.
    /// |e| ≤ B/6: trapezio σ = N/B·(1 ± 6e/B). Oltre il nocciolo: triangolo di lunghezza 3u (o 3(B−u)), σmax = 2N/(3u).
    /// Si trascura il momento della reazione orizzontale (base del muro a quota y = 0 del piede).
    /// </summary>
    public static WallBasePressure BasePressure(IReadOnlyList<WallForce> forces, double b)
    {
        var n = -forces.Sum(f => f.Fy); var m = forces.Sum(f => f.Mz);
        if (n <= 1e-9) return new(n, double.NaN, double.NaN, b, 0, null, null, []);
        var u = -m / n; var e = b / 2 - u;
        if (u <= 0 || u >= b) return new(n, u, e, b, 0, null, null, []);
        if (Math.Abs(e) <= b / 6)
        {
            double toe = n / b * (1 + 6 * e / b), heel = n / b * (1 - 6 * e / b);
            return new(n, u, e, b, b, toe, heel, [new(0, toe), new(b, heel)]);
        }
        if (e > 0)
        {
            var length = 3 * u; var max = 2 * n / length;
            return new(n, u, e, b, length, max, 0, [new(0, max), new(length, 0), new(b, 0)]);
        }
        var back = 3 * (b - u); var maxHeel = 2 * n / back;
        return new(n, u, e, b, back, 0, maxHeel, [new(0, 0), new(b - back, 0), new(b, maxHeel)]);
    }

    /// <summary>Diagramma della pressione orizzontale statica caratteristica (terreno + sovraccarico, M1) sul piano x = Bf.</summary>
    private static IReadOnlyList<WallPressurePoint> Pressures(IReadOnlyDictionary<string,double> d, WallGeometry g)
    {
        var top = g.BackSoil[2].Y; var water = Math.Min(d["Qfp"], top); var bottom = g.Wall[11].Y;
        var ka = Ka(Rad(d["FI_T"]), Rad(d["Delta_T_M"]), Math.Atan(d["Itp"] / 100), 0);
        var levels = new[] { top, water, g.Wall[1].Y, g.Wall[9].Y, bottom }.Where(y => y <= top && y >= bottom).Distinct().OrderDescending();
        return levels.Select(y => new WallPressurePoint(y, ka * (SigmaV(y, top, water, d["GammaTerreno"]) + d["Sovraccarico"]))).ToArray();
    }

    private static IReadOnlyList<string> Notes(IReadOnlyDictionary<string,double> d, double kh)
    {
        List<string> notes = [Scope,
            "Spinte con le formule (F.1)–(F.4) del sorgente sul piano verticale virtuale per l'estremità del tallone (x = Bf), interpretate come componente orizzontale; componente verticale = orizzontale × tan δ.",
            "Ribaltamento attorno al piede anteriore (origine). Per ogni azione il coefficiente parziale è scelto in base all'effetto netto (favorevole/sfavorevole).",
            "Scorrimento sul piano orizzontale per la base del dente, con resistenza N·tan φ′ e il 50 % della resistenza passiva a valle (γR = 1,4 statica, 1,2 sisma). Il peso del sovraccarico non è portato in conto.",
            "Acqua: pressioni idrostatiche a monte (Qfp) e a valle (Qfa), sottospinta con quota piezometrica lineare fra valle e monte; terreno sotto falda con γ′ = γ − γw (γsat ≈ γ). Assenti sovrappressioni idrodinamiche in sisma.",
            "Sisma: kh = αH = B_H·PGA/χ (PGA = amax/g comprensiva dell'amplificazione locale), kv = ±0,5·kh, θ = atan[kh/(1 ± kv)], inerzie di muro e terreno sulla mensola. La spinta sismica segue la distribuzione della statica.",
            "La coesione c′ₖ non è utilizzata (a favore di sicurezza).",
            "Il ramo di B_H fra H/(Vs·Tm) = 0,05 e 0,4 è stato reso continuo (fino a 0,4): verificare la fonte normativa del coefficiente."];
        if (d["Vs"] < 10) notes.Add("Vs è inferiore a 10 m/s: il valore 0,209 del programma originale è probabilmente in km/s. Con Vs in m/s B_H assume il valore minimo 0,4.");
        if (d["Qfp"] > d["H2"] + d["Htp"] || d["Qfa"] > d["H2"] + d["Hta"]) notes.Add("Una quota di falda supera la superficie del terreno: la parte d'acqua sopra il terreno non è considerata.");
        if (kh == 0) notes.Add("PGA nulla: le combinazioni sismiche coincidono con la statica a coefficienti unitari.");
        return notes;
    }
}
