using STA.MuroSostegno.Core.Legacy;
namespace STA.MuroSostegno.Core;

/// <summary>Coordinate reali in metri; l'origine coincide con il bordo anteriore inferiore della fondazione.</summary>
public readonly record struct WallPoint(double X, double Y);
public sealed record PolygonProperties(double Area, WallPoint Centroid);
public sealed record WallGeometry(WallPoint[] Wall, WallPoint[] FrontSoil, WallPoint[] BackSoil,
    double FrontWater, double BackWater)
{
    internal static Punto[] LegacyPoints(IEnumerable<WallPoint> points) => points.Select(p => new Punto { x = p.X, y = p.Y }).ToArray();
    public static PolygonProperties Properties(WallPoint[] points)
    {
        new GeomAnalitica().CalcolaAreaEBaricentro(LegacyPoints(points), out var area, out var center);
        return new(area, new(center.x, center.y));
    }
    /// <summary>Riproduce i quattordici vertici utili del sorgente; omette solo gli zeri finali coincidenti con l'origine.</summary>
    public static WallGeometry Create(IReadOnlyDictionary<string,double> d)
    {
        WallValidation.Validate(d);
        var h = d["H1"]; var h2 = d["H2"]; var ba = d["Ba"]; var b = d["Bf"];
        var w = new WallPoint[14];
        w[0] = new(0, 0); w[1] = new(0, h2); w[2] = new(ba, h2);
        w[4] = new(ba + d["Ds1"], h2 + h);
        w[3] = new(ba + d["Ds1"] * d["Hta"] / h, h2 + d["Hta"]);
        w[5] = new(w[4].X + d["S1"], w[4].Y);
        w[7] = new(ba + d["S2"], h2);
        w[6] = new(w[5].X + (w[7].X - w[5].X) * (h - d["Htp"]) / h, h2 + d["Htp"]);
        w[8] = new(b, h2); w[9] = new(b, h2 - d["H3"]);
        w[10] = new(d["Xd"] + d["S3"], w[9].Y);
        w[11] = new(w[10].X, w[9].Y - d["H4"]);
        w[12] = new(d["Xd"], w[11].Y); w[13] = new(d["Xd"], w[9].Y);
        WallPoint[] front = [w[1], w[2], w[3], new(0, w[3].Y - d["It"] / 100 * w[3].X)];
        WallPoint[] back = [w[7], w[8], new(b, w[6].Y + d["Itp"] / 100 * (b - w[6].X)), w[6]];
        return new(w, front, back, d["Qfa"], d["Qfp"]);
    }
}

/// <summary>Controlli aggiunti nell'adattamento: mai sostituire un input errato con un valore predefinito.</summary>
public static class WallValidation
{
    public static void Validate(IReadOnlyDictionary<string,double> d)
    {
        foreach (var p in WallParameters.All)
            if (!d.TryGetValue(p.Key, out var v) || !double.IsFinite(v))
                throw new ArgumentException($"{p.Label}: inserire un numero finito.");
        void Require(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
        foreach (var key in new[] { "H1", "S1", "S2", "H2", "H3", "S3", "Bf", "GammaMuro", "GammaTerreno", "Vs", "Csi_H", "Tm" })
            Require(d[key] > 0, $"{key}: il valore deve essere maggiore di zero.");
        foreach (var key in new[] { "Ba", "H4", "Xd", "Hta", "Htp", "Sovraccarico", "C1k", "PGA" })
            Require(d[key] >= 0, $"{key}: il valore non può essere negativo.");
        Require(d["Hta"] <= d["H1"] && d["Htp"] <= d["H1"], "Le altezze dei terreni non possono superare H1.");
        Require(d["Ba"] + d["S2"] <= d["Bf"], "La larghezza Bf deve contenere lo sporto Ba e lo spessore S2.");
        Require(d["Ba"] + d["Ds1"] >= 0 && d["Ba"] + d["Ds1"] + d["S1"] <= d["Bf"], "La sommità del muro deve ricadere sulla fondazione.");
        Require(d["Xd"] + d["S3"] <= d["Bf"], "Il dente deve ricadere nella fondazione: Xd + S3 ≤ Bf.");
        Require(d["FI_T"] > 0 && d["FI_T"] < 90, "L'angolo φ′ deve essere compreso tra 0° e 90° esclusi.");
        Require(Math.Abs(d["Delta_T_M"]) <= d["FI_T"], "L'attrito muro-terreno δ non può superare φ′ in valore assoluto.");
        Require(d["Psi_Sovr"] >= 0 && d["Psi_Sovr"] <= 1, "Ψ del sovraccarico deve essere compreso tra 0 e 1.");
        var frontX = d["Ba"] + d["Ds1"] * d["Hta"] / d["H1"];
        var backX = d["Ba"] + d["S2"] + (d["Ds1"] + d["S1"] - d["S2"]) * d["Htp"] / d["H1"];
        Require(d["Hta"] - d["It"] / 100 * frontX >= -1e-10, "La superficie del terreno anteriore interseca la fondazione.");
        Require(d["Htp"] + d["Itp"] / 100 * (d["Bf"] - backX) >= -1e-10, "La superficie del terreno posteriore interseca la fondazione.");
    }
}
