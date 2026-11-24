namespace STA.Plinti.Core;

// ─────────────────────────────────────────────────────────────────────────────
// Motore RIVISTO dei plinti superficiali (NTC 2018 §6.4.2, EC7 Annex D, EC2 §6.2 e §6.4.4).
// Il motore del prototipo (FootingCalculator) resta invariato per la CLI e per la parità con il prototipo web.
// Differenze rispetto al prototipo:
//  1. pressioni sul terreno anche con base parzializzata (piano delle tensioni non reagente a trazione, biassiale);
//  2. capacità portante calcolata (EC7 Annex D, drenata) con γR, oppure qRd assegnata;
//  3. scorrimento con γR = 1,1 e pesi propri favorevoli;
//  4. flessione e taglio con la pressione netta (pressione − peso proprio e rinterro) integrata sulla mensola;
//  5. punzonamento EC2 §6.4.4 sui perimetri a ≤ 2d con resistenza aumentata 2d/a e riduzione per la pressione interna;
//  6. braccio interno dalla soluzione del blocco rettangolare (0,8x) invece di z = 0,9d fisso.
// Unità: kN, m, kPa; materiali MPa; armature mm²/m. Assi: x lungo B, y lungo L, origine al centro della base.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Esito di una verifica: Rd/Ed ≥ 1 se soddisfatta; Ratio null se l'azione è nulla.</summary>
public sealed record FootingCheck(string Name, double Demand, double Capacity, string Unit, string Note)
{
    public double? Ratio => Demand > 1e-12 ? Capacity / Demand : null;
    public bool Ok => Ratio is not double r || r >= 1 - 1e-12;
}

/// <summary>Piano delle tensioni σ(x,y) = A + Bx·x + By·y [kPa], efficace solo dove positivo.</summary>
public sealed record PressurePlane(double A, double Bx, double By)
{
    public double At(double x, double y) => Math.Max(0, A + Bx * x + By * y);
}

/// <summary>Distribuzione di contatto sulla base B×L: piano, pressione massima e minima ai vertici, quota reagente.</summary>
public sealed record FootingPressure(bool Valid, PressurePlane Plane, double SigmaMax, double SigmaMin, double ContactRatio, string Message);

public sealed record FootingDesignResult(int Node, string Case, string LimitState, double N, double NFav, double Horizontal, double Mx, double My,
    double Ex, double Ey, double BEff, double LEff, double Weight, double D, FootingPressure Pressure, double QLim,
    double MomentX, double MomentY, double AsReqX, double AsReqY, double AsProvided,
    IReadOnlyList<FootingCheck> Checks, IReadOnlyList<string> Issues)
{
    public bool Ok => Checks.All(c => c.Ok) && Pressure.Valid;
    /// <summary>Verifica più gravosa (minimo Rd/Ed).</summary>
    public FootingCheck Governing => Checks.Where(c => c.Ratio is not null).MinBy(c => c.Ratio);
}

public static class FootingDesignCalculator
{
    public const string Scope = "Motore rivisto NTC 2018 / EC2 / EC7: pressioni con base parzializzata, capacità portante EC7 Annex D (drenata), scorrimento γR, flessione e taglio con pressione netta, punzonamento EC2 §6.4.4. Esclusi cedimenti, fessurazione e dettagli costruttivi.";
    private const int Grid = 120;
    private static readonly System.Globalization.CultureInfo It = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
    /// <summary>Numeri delle note sempre in formato italiano, indipendentemente dalla lingua del sistema.</summary>
    private static string F(double v, string format) => v.ToString(format, It);

    public static void Validate(FootingParameters p)
    {
        FootingCalculator.Validate(p);
        void Require(bool ok, string m) { if (!ok) throw new ArgumentException(m); }
        Require(double.IsFinite(p.Depth) && p.Depth >= 0, "Profondità del piano di posa non valida.");
        Require(double.IsFinite(p.SoilWeight) && p.SoilWeight is > 0 and < 30, "Peso del terreno non valido (0–30 kN/m³).");
        Require(p.Bearing is "assigned" or "computed", "Capacità portante: 'assigned' oppure 'computed'.");
        Require(double.IsFinite(p.Phi) && p.Phi is >= 0 and < 50, "φ′ deve essere compreso fra 0 e 50°.");
        Require(p.Bearing == "assigned" || p.Phi >= 1, "Capacità portante calcolata: condizioni drenate con φ′ ≥ 1°.");
        Require(double.IsFinite(p.Cohesion) && p.Cohesion >= 0, "Coesione non valida.");
        Require(double.IsFinite(p.GammaRBearing) && p.GammaRBearing >= 1 && double.IsFinite(p.GammaRSliding) && p.GammaRSliding >= 1, "γR non validi.");
        Require(double.IsFinite(p.GammaGFav) && p.GammaGFav is > 0 and <= 1, "γG favorevole fra 0 e 1.");
    }

    public static IReadOnlyList<FootingDesignResult> Calculate(IReadOnlyList<STA.Calcolo.Contracts.NodeReaction> rows, FootingParameters p)
    {
        Validate(p);
        STA.Calcolo.Contracts.ReactionValidation.Validate(rows);
        return rows.Select(r => Check(r, p)).ToArray();
    }

    /// <summary>Profondità efficace del piano di posa: almeno lo spessore del plinto.</summary>
    public static double EffectiveDepth(FootingParameters p) => Math.Max(p.Depth, p.H);

    // ── Pressioni di contatto ───────────────────────────────────────────────
    /// <summary>
    /// Piano delle tensioni non reagente a trazione che equilibra N con eccentricità (ex, ey) sulla base B×L.
    /// Con la risultante nel nocciolo coincide con Navier; altrimenti è risolto con Newton sulle celle reagenti
    /// (integrazione al punto medio su una griglia Grid×Grid: esatta per le celle interamente compresse).
    /// </summary>
    public static FootingPressure Pressure(double b, double l, double n, double ex, double ey)
    {
        if (!(n > 0)) return new(false, new(0, 0, 0), 0, 0, 0, "Risultante verticale non compressiva: sollevamento.");
        if (Math.Abs(ex) >= b / 2 || Math.Abs(ey) >= l / 2) return new(false, new(0, 0, 0), 0, 0, 0, "Risultante esterna alla base: ribaltamento.");
        double area = b * l, ix = b * l * l * l / 12, iy = l * b * b * b / 12;
        // Navier: valido se tutti i vertici risultano compressi.
        var plane = new PressurePlane(n / area, n * ex / iy, n * ey / ix);
        double Corner(PressurePlane pl, double sx, double sy) => pl.A + pl.Bx * sx * b / 2 + pl.By * sy * l / 2;
        var corners = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };
        if (corners.All(c => Corner(plane, c.Item1, c.Item2) >= -1e-9))
        {
            var values = corners.Select(c => Corner(plane, c.Item1, c.Item2)).ToArray();
            return new(true, plane, values.Max(), values.Min(), 1, "");
        }
        // Newton con ricerca lineare sul residuo delle tre equazioni di equilibrio.
        double dx = b / Grid, dy = l / Grid, da = dx * dy;
        double[] Residual(PressurePlane pl, out double[,] jac, out int active)
        {
            double f0 = 0, f1 = 0, f2 = 0; jac = new double[3, 3]; active = 0;
            for (var i = 0; i < Grid; i++)
            {
                var x = -b / 2 + (i + .5) * dx;
                for (var j = 0; j < Grid; j++)
                {
                    var y = -l / 2 + (j + .5) * dy; var s = pl.A + pl.Bx * x + pl.By * y;
                    if (s <= 0) continue;
                    active++; f0 += s * da; f1 += s * x * da; f2 += s * y * da;
                    double[] v = [1, x, y];
                    for (var r = 0; r < 3; r++) for (var c = 0; c < 3; c++) jac[r, c] += v[r] * v[c] * da;
                }
            }
            return [f0 - n, f1 - n * ex, f2 - n * ey];
        }
        double Norm(double[] f) => Math.Sqrt(f[0] * f[0] + (f[1] * f[1] + f[2] * f[2]) / (b * l));
        var res = Residual(plane, out var jacobian, out var act);
        for (var iter = 0; iter < 200 && Norm(res) > 1e-9 * n; iter++)
        {
            if (act < 3) break;
            var step = Solve3(jacobian, res);
            if (step is null) break;
            var t = 1.0; PressurePlane next; double[] nextRes; double[,] nextJac; int nextAct;
            do
            {
                next = new(plane.A - t * step[0], plane.Bx - t * step[1], plane.By - t * step[2]);
                nextRes = Residual(next, out nextJac, out nextAct); t /= 2;
            } while (Norm(nextRes) >= Norm(res) && t > 1e-6);
            plane = next; res = nextRes; jacobian = nextJac; act = nextAct;
        }
        if (Norm(res) > 1e-6 * n) return new(false, plane, 0, 0, 0, "Pressione non determinabile: risultante troppo vicina al bordo della base.");
        var cornerValues = corners.Select(c => Math.Max(0, Corner(plane, c.Item1, c.Item2))).ToArray();
        return new(true, plane, cornerValues.Max(), cornerValues.Min(), (double)act / (Grid * Grid), "Base parzializzata: terreno non reagente a trazione.");
    }

    private static double[] Solve3(double[,] m, double[] r)
    {
        double Det(double[,] a) => a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1]) - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0]) + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);
        var det = Det(m); if (Math.Abs(det) < 1e-30) return null;
        var x = new double[3];
        for (var k = 0; k < 3; k++)
        {
            var c = (double[,])m.Clone(); for (var i = 0; i < 3; i++) c[i, k] = r[i];
            x[k] = Det(c) / det;
        }
        return x;
    }

    /// <summary>Integrale al punto medio di f(x,y) sul rettangolo [x0,x1]×[y0,y1] (nx×ny celle).</summary>
    private static double Integrate(Func<double, double, double> f, double x0, double x1, double y0, double y1, int nx = 200, int ny = 120)
    {
        if (x1 <= x0 || y1 <= y0) return 0;
        double dx = (x1 - x0) / nx, dy = (y1 - y0) / ny, sum = 0;
        for (var i = 0; i < nx; i++) for (var j = 0; j < ny; j++) sum += f(x0 + (i + .5) * dx, y0 + (j + .5) * dy);
        return sum * dx * dy;
    }

    // ── Capacità portante ───────────────────────────────────────────────────
    /// <summary>
    /// Carico limite drenato EC7 Annex D (formula D.2) sulla base efficace B′×L′, senza fattori di profondità (cautelativo):
    /// qlim = c′Nc·sc·ic + q′Nq·sq·iq + 0,5·γ·B′·Nγ·sγ·iγ, con Nq = e^(π tanφ)·tan²(45°+φ/2), Nc = (Nq−1)cotφ, Nγ = 2(Nq−1)tanφ.
    /// </summary>
    public static double BearingLimit(double phiDeg, double c, double gamma, double depth, double b1, double l1, double v, double hShort, double hLong)
    {
        if (b1 <= 0 || l1 <= 0 || v <= 0) return 0;
        double bs = Math.Min(b1, l1), ls = Math.Max(b1, l1), phi = phiDeg * Math.PI / 180, tan = Math.Tan(phi), r = bs / ls;
        double nq = Math.Exp(Math.PI * tan) * Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2), nc = (nq - 1) / tan, ng = 2 * (nq - 1) * tan;
        double sq = 1 + r * Math.Sin(phi), sg = 1 - .3 * r, sc = (sq * nq - 1) / (nq - 1);
        // Inclinazione: m dipende dalla direzione di H rispetto ai lati (EC7 D.4, interpolazione con l'angolo θ dal lato lungo).
        double h = Math.Sqrt(hShort * hShort + hLong * hLong), mB = (2 + r) / (1 + r), mL = (2 + 1 / r) / (1 + 1 / r);
        double theta = h > 0 ? Math.Atan2(hShort, hLong) : 0, m = mL * Math.Pow(Math.Cos(theta), 2) + mB * Math.Pow(Math.Sin(theta), 2);
        double baseTerm = Math.Max(1e-9, 1 - h / (v + bs * ls * c / tan));
        double iq = Math.Pow(baseTerm, m), ig = Math.Pow(baseTerm, m + 1), ic = iq - (1 - iq) / (nc * tan);
        return c * nc * sc * ic + gamma * depth * nq * sq * iq + .5 * gamma * bs * ng * sg * ig;
    }

    // ── Verifiche per una sestupla ──────────────────────────────────────────
    private static FootingDesignResult Check(STA.Calcolo.Contracts.NodeReaction r, FootingParameters p)
    {
        var uls = p.LimitState == "ULS";
        double depth = EffectiveDepth(p), backfill = Math.Max(0, depth - p.H);
        // Pesi: plinto, rinterro sopra la soletta (escluso il pilastro) e pesi aggiuntivi.
        var weight = p.B * p.L * p.H * p.Density + (p.B * p.L - p.Cx * p.Cy) * backfill * p.SoilWeight + p.ExtraWeight;
        double gUnf = uls ? p.GammaG : 1, gFav = uls ? p.GammaGFav : 1;
        var n = r.FZ + gUnf * weight; var nFav = r.FZ + gFav * weight;
        // Azioni alla base (stessa convenzione del prototipo: reazioni del vincolo sulla struttura, trasportate alla base).
        var mx = -r.MX + p.NodeHeight * r.FY; var my = -r.MY - p.NodeHeight * r.FX; var horizontal = Math.Sqrt(r.FX * r.FX + r.FY * r.FY);
        double ex = n > 0 ? my / n : double.PositiveInfinity, ey = n > 0 ? mx / n : double.PositiveInfinity;
        double be = p.B - 2 * Math.Abs(ex), le = p.L - 2 * Math.Abs(ey);
        var pressure = Pressure(p.B, p.L, n, ex, ey);
        var checks = new List<FootingCheck>(); var issues = new List<string>();
        if (!pressure.Valid) issues.Add(pressure.Message);
        else if (pressure.ContactRatio < 1) issues.Add($"Base parzializzata: area reagente {F(pressure.ContactRatio,"P0")}.");
        if (Math.Abs(r.MZ) > 1e-6) issues.Add("Torsione MZ non verificata.");

        // Capacità portante (o pressione ammissibile in esercizio).
        double qlim = 0; var area1 = be > 0 && le > 0 ? be * le : 0;
        if (uls)
        {
            if (p.Bearing == "computed")
            {
                // H scomposta lungo il lato corto e il lato lungo della base efficace.
                double hx = Math.Abs(r.FX), hy = Math.Abs(r.FY);
                var (hShort, hLong) = be <= le ? (hx, hy) : (hy, hx);
                qlim = BearingLimit(p.Phi, p.Cohesion, p.SoilWeight, depth, be, le, n, hShort, hLong);
                checks.Add(new("Capacità portante", n, qlim * area1 / p.GammaRBearing, "kN", $"qlim = {F(qlim,"0")} kPa su B′·L′ = {F(area1,"0.00")} m², γR = {F(p.GammaRBearing,"0.0#")}"));
            }
            else checks.Add(new("Capacità portante", n, p.QRd * area1, "kN", $"qRd assegnata = {F(p.QRd,"0")} kPa su B′·L′ = {F(area1,"0.00")} m²"));
            checks.Add(new("Scorrimento", horizontal, Math.Max(0, p.Mu * nFav) / p.GammaRSliding, "kN", $"μ·N(pesi favorevoli ×{F(gFav,"0.0#")}) / γR = {F(p.GammaRSliding,"0.0#")}"));
        }
        else
        {
            if (p.Bearing == "assigned") checks.Add(new("Pressione massima in esercizio", pressure.SigmaMax, p.QRd, "kPa", "σmax con terreno non reagente a trazione ≤ q ammissibile assegnata"));
            issues.Add("SLE: pressioni di esercizio; cedimenti e fessurazione non calcolati.");
        }

        // Verifiche strutturali (solo SLU e con pressione determinata).
        var d = p.H - p.Cover / 1000 - 1.5 * p.Diameter / 1000;
        double fcd = p.AlphaCC * p.Fck / p.GammaC, fyd = p.Fyk / p.GammaS;
        var steel = 1000 / p.Spacing * Math.PI * p.Diameter * p.Diameter / 4; // mm²/m
        var fctm = .3 * Math.Pow(p.Fck, 2d / 3); var asMin = Math.Max(.26 * fctm / p.Fyk, .0013) * 1000 * d * 1000;
        double momentX = 0, momentY = 0, asReqX = asMin, asReqY = asMin;
        if (uls && pressure.Valid)
        {
            // Pressione netta verso l'alto: pressione di contatto meno peso del plinto e del rinterro sulla mensola [kPa].
            var w = gUnf * (p.H * p.Density + backfill * p.SoilWeight);
            double Net(double x, double y) => pressure.Plane.At(x, y) - w;
            double ax = (p.B - p.Cx) / 2, ay = (p.L - p.Cy) / 2;
            // Momento alla sezione di filo pilastro per metro di larghezza, massimo fra i due lati.
            double Cantilever(bool alongX, int side, double offset, bool moment)
            {
                double face = (alongX ? p.Cx : p.Cy) / 2 + offset, edge = (alongX ? p.B : p.L) / 2, width = alongX ? p.L : p.B;
                if (edge <= face) return 0;
                double Value(double s, double t) => Net(alongX ? side * s : t, alongX ? t : side * s) * (moment ? s - (alongX ? p.Cx : p.Cy) / 2 : 1);
                return Integrate(Value, face, edge, -width / 2, width / 2) / width;
            }
            momentX = Math.Max(Cantilever(true, 1, 0, true), Cantilever(true, -1, 0, true));
            momentY = Math.Max(Cantilever(false, 1, 0, true), Cantilever(false, -1, 0, true));
            if (Math.Min(Cantilever(true, 1, 0, true), Cantilever(true, -1, 0, true)) < -1e-6 || Math.Min(Cantilever(false, 1, 0, true), Cantilever(false, -1, 0, true)) < -1e-6)
                issues.Add("Momento negativo su una mensola (base parzializzata): verificare l'armatura superiore.");
            FootingCheck Flexure(string name, double m, out double asReq)
            {
                // Blocco rettangolare 0,8x a fcd su 1 m: y = d(1 − √(1 − 2m/(fcd·b·d²))), z = d − y/2, x/d ≤ 0,45.
                double dm = d * 1000, mNmm = Math.Max(0, m) * 1e6, k = mNmm / (fcd * 1000 * dm * dm);
                if (2 * k >= 1) { asReq = double.PositiveInfinity; return new(name, 1, 0, "mm²/m", "Sezione insufficiente: compressione oltre il blocco rettangolare."); }
                double yb = dm * (1 - Math.Sqrt(1 - 2 * k)), z = dm - yb / 2, xi = yb / .8 / dm;
                asReq = Math.Max(asMin, mNmm / (z * fyd));
                return new(name, asReq, steel, "mm²/m", $"m = {F(m,"0.0")} kN·m/m, z = {F(z / dm,"0.000")}·d, x/d = {F(xi,"0.00")}{(xi > .45 ? " > 0,45: duttilità insufficiente" : "")}");
            }
            var fx = Flexure("Flessione direzione X", momentX, out asReqX); var fy = Flexure("Flessione direzione Y", momentY, out asReqY);
            if (fx.Note.Contains("> 0,45") || fy.Note.Contains("> 0,45")) issues.Add("x/d > 0,45: aumentare lo spessore.");
            checks.Add(fx); checks.Add(fy);
            // Taglio senza armature a distanza d dal filo (EC2 6.2.2).
            var rho = Math.Min(steel / (1000 * d * 1000), .02); var k2 = Math.Min(1 + Math.Sqrt(200 / (d * 1000)), 2);
            var vRdc = Math.Max(.18 / p.GammaC * k2 * Math.Cbrt(100 * rho * p.Fck), .035 * Math.Pow(k2, 1.5) * Math.Sqrt(p.Fck));
            double Shear(bool alongX) => Math.Max(Cantilever(alongX, 1, d, false), Cantilever(alongX, -1, d, false)) / (d * 1000);
            checks.Add(new("Taglio direzione X", Shear(true), vRdc, "MPa", "a distanza d dal filo pilastro"));
            checks.Add(new("Taglio direzione Y", Shear(false), vRdc, "MPa", "a distanza d dal filo pilastro"));
            // Punzonamento EC2 §6.4.4: perimetri a ∈ (0, 2d], riduzione per la pressione netta interna, resistenza ×2d/a.
            if (r.FZ > 0)
            {
                double best = double.PositiveInfinity, bestA = 0, bestV = 0, bestR = 0;
                for (var a = d / 10; a <= 2 * d + 1e-9; a += d / 10)
                {
                    if (a > Math.Min(ax, ay)) break; // perimetro oltre il bordo: il carico esce dal plinto
                    double u = 2 * (p.Cx + p.Cy) + 2 * Math.PI * a;
                    bool Inside(double x, double y)
                    {
                        double qx = Math.Max(0, Math.Abs(x) - p.Cx / 2), qy = Math.Max(0, Math.Abs(y) - p.Cy / 2);
                        return qx * qx + qy * qy <= a * a;
                    }
                    double hx = p.Cx / 2 + a, hy = p.Cy / 2 + a;
                    var dv = Integrate((x, y) => Inside(x, y) ? Net(x, y) : 0, -hx, hx, -hy, hy, 120, 120);
                    var vEd = p.Beta * Math.Max(0, r.FZ - dv) / (u * d * 1000);
                    var vRd = vRdc * 2 * d / a;
                    var ratio = vEd > 0 ? vRd / vEd : double.PositiveInfinity;
                    if (ratio < best) { best = ratio; bestA = a; bestV = vEd; bestR = vRd; }
                }
                if (double.IsFinite(best)) checks.Add(new("Punzonamento", bestV, bestR, "MPa", $"perimetro critico a = {F(bestA / d,"0.0")}·d, β = {F(p.Beta,"0.0#")}"));
                var u0 = 2 * (p.Cx + p.Cy); var nu = .6 * (1 - p.Fck / 250);
                checks.Add(new("Punzonamento al filo pilastro", p.Beta * r.FZ / (u0 * d * 1000), .4 * nu * fcd, "MPa", "vRd,max = 0,4·ν·fcd"));
                var betaMin = 1 + 1.8 * Math.Sqrt(Math.Pow(r.MY / (r.FZ * p.Cx), 2) + Math.Pow(r.MX / (r.FZ * p.Cy), 2));
                if (p.Beta < betaMin) issues.Add($"β assegnato inferiore alla stima per eccentricità del pilastro ({F(betaMin,"0.00")}).");
            }
            checks.Add(new("Passo armatura", p.Spacing, Math.Min(3 * p.H * 1000, 400), "mm", "passo ≤ min(3h, 400 mm)"));
        }
        return new(r.Node, r.Case, p.LimitState, n, nFav, horizontal, mx, my, ex, ey, be, le, weight, d, pressure, qlim,
            momentX, momentY, asReqX, asReqY, steel, checks, issues);
    }
}
