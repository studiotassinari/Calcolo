namespace STA.Taglio.Core;

/// <summary>
/// Port of studiotassinari/TaglioClsRect, ClsTaglio.cs.
/// Preserves the source's formula variants, 22.5–45° search and wall factor.
/// This migration is not a normative revision; see docs/TAGLIO-CLS.md.
/// All resistance outputs are kN. No mutable intermediate calculation state.
/// </summary>
public static class ShearCalculator
{
    public static void Validate(ShearInput p)
    {
        ArgumentNullException.ThrowIfNull(p);
        static void Range(double n, double lo, double hi, string name)
        {
            if (!double.IsFinite(n) || n < lo || n > hi)
                throw new ArgumentException($"{name}: inserire un valore tra {lo} e {hi}.");
        }
        Range(p.H, 1, 100000, "H [mm]"); Range(p.B, 1, 100000, "b [mm]");
        Range(p.Cover, 0, p.H - .001, "Distanza armatura tesa [mm]");
        Range(p.Asl, 0, p.B * p.H, "Asl [mm²]");
        Range(p.Fck, 8, 90, "fck [MPa]"); Range(p.Fyk, 1, 1000, "fyk [MPa]");
        Range(p.GammaC, 1, 3, "γc"); Range(p.GammaS, 1, 3, "γs");
        Range(p.AlphaCc, .1, 1, "αcc"); Range(p.N, 0, 1e9, "N compressione [kN]");
        Range(p.V, 0, 1e9, "Valore assoluto VEd [kN]"); Range(p.Theta, 22.5, 45, "θ [°]");
        Range(p.Legs, 0, 100, "Numero bracci"); Range(p.BentCount, 0, 100, "Numero piegati");
        if (p.Legs != Math.Truncate(p.Legs) || p.BentCount != Math.Truncate(p.BentCount))
            throw new ArgumentException("Il numero di bracci e di piegati deve essere intero.");
        Range(p.Diameter, p.Legs > 0 ? 1 : 0, 100, "Diametro staffe");
        Range(p.Spacing, p.Legs > 0 ? 1 : 0, 100000, "Interasse staffe");
        Range(p.Alpha, 45, 90, "α staffe");
        Range(p.BentDiameter, p.BentCount > 0 ? 1 : 0, 100, "Diametro piegati");
        Range(p.BentSpacing, p.BentCount > 0 ? 1 : 0, 100000, "Interasse piegati");
        Range(p.BentAlpha, 45, 90, "α piegati");
    }

    private static double Cot(double degrees) => 1 / Math.Tan(degrees * Math.PI / 180);
    private static double Reinforcement(ShearInput p, double theta)
    {
        double Contribution(double count, double diameter, double spacing, double alpha)
        {
            if (count == 0) return 0; // No 0/0 for disabled reinforcement.
            var angle = alpha * Math.PI / 180;
            return .9 * (p.H - p.Cover) * count * Math.PI * diameter * diameter / 4
                * (p.Fyk / p.GammaS) / spacing
                * (Cot(theta) * Math.Sin(angle) + Math.Cos(angle)) / 1000;
        }
        return Contribution(p.Legs, p.Diameter, p.Spacing, p.Alpha)
            + Contribution(p.BentCount, p.BentDiameter, p.BentSpacing, p.BentAlpha);
    }
    private static double AlphaCw(ShearInput p)
    {
        var ratio = p.N * 1000 / (p.B * p.H) / (p.Fck * p.AlphaCc / p.GammaC);
        return ratio <= .25 ? 1 + ratio : ratio <= .5 ? 1.25 : ratio < 1 ? 2.5 * (1 - ratio) : 0;
    }
    private static double Strut(ShearInput p, double theta)
    {
        var nu = p.Eurocode ? Math.Max(.5, p.Fck > 60 ? .9 - p.Fck / 200 : .6) : .5;
        var ct = Cot(theta);
        return .9 * (p.H - p.Cover) * p.B * AlphaCw(p)
            * (p.Fck * p.AlphaCc / p.GammaC) * nu * (ct + Cot(p.Alpha)) / (1 + ct * ct)
            * (p.Wall ? .4 : 1) / 1000;
    }
    private static double SuggestTheta(ShearInput p)
    {
        // Preserve the original NTC wall convention (45°), without side effects.
        if (p.Wall && !p.Eurocode) return 45;
        if (Reinforcement(p, 22.5) <= Strut(p, 22.5)) return 22.5;
        if (Reinforcement(p, 45) >= Strut(p, 45)) return 45;
        double lo = 22.5, hi = 45;
        for (int i = 0; i < 64; i++)
        {
            var mid = (lo + hi) / 2;
            if (Strut(p, mid) - Reinforcement(p, mid) > 0) hi = mid; else lo = mid;
        }
        return (lo + hi) / 2;
    }
    public static ShearResult Calculate(ShearInput p)
    {
        Validate(p);
        double d = p.H - p.Cover, fcd = p.Fck * p.AlphaCc / p.GammaC;
        double k = Math.Min(2, 1 + Math.Sqrt(200 / d)), rho = Math.Min(.02, p.Asl / (p.B * d));
        // Deliberately preserve the source's bw*d denominator for VRd,c.
        double sigma = Math.Min(.2 * fcd, p.N * 1000 / (p.B * d));
        double concrete = Math.Max(.18 / p.GammaC * k * Math.Cbrt(100 * rho * p.Fck),
            .035 * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck));
        concrete = (concrete + .15 * sigma) * p.B * d / 1000;
        double steel = Reinforcement(p, p.Theta), strut = Strut(p, p.Theta);
        bool reinforced = p.Legs + p.BentCount > 0;
        double capacity = reinforced ? Math.Min(steel, strut) : concrete;
        double theta = reinforced ? SuggestTheta(p) : p.Theta;
        double suggested = reinforced ? Math.Min(Reinforcement(p, theta), Strut(p, theta)) : concrete;
        double? utilization = capacity > 0 ? p.V / capacity : null;
        return new(d, fcd, p.Fyk / p.GammaS, rho, k, sigma, AlphaCw(p), p.Wall ? .4 : 1,
            concrete, steel, strut, capacity, utilization, capacity > 0 && p.V <= capacity, theta, suggested);
    }
}
