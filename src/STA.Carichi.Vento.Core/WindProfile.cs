namespace Vento;

/// <summary>Parametri immutabili NTC 2018, tabella 3.3.II; lunghezze in metri.</summary>
public sealed record ExposureParameters(string Category, double Kr, double Z0, double Zmin);

/// <summary>Funzioni pure per il vento. Non usa le tabelle geografiche del modello NTC 2008.</summary>
public static class WindProfile
{
    public static IReadOnlyList<ExposureParameters> Categories { get; } = Array.AsReadOnly(new ExposureParameters[]
    {
        new("I", .17, .01, 2), new("II", .19, .05, 4), new("III", .20, .10, 5),
        new("IV", .22, .30, 8), new("V", .23, .70, 12)
    });
    /// <summary>Velocità base in m/s: vb = vb0 [1 + ks (as/a0 − 1)] sopra a0, TR = 50 anni.</summary>
    public static double BaseVelocity(Vento.Zone zone, double altitude)
    {
        Check(altitude, 0, 1500, "Quota sito");
        var reference = new GestioneZonaGeografica(zone);
        double[] ks = [.40, .45, .37, .36, .40, .36, .54, .50, .32];
        return reference.Vb0 * (altitude <= reference.a0 ? 1 : 1 + ks[(int)zone] * (altitude / reference.a0 - 1));
    }
    /// <summary>Ce(z) mantiene il plateau sotto zmin; ct è il coefficiente topografico, non termico.</summary>
    public static double Exposure(string category, double z, double ct = 1)
    {
        Check(z, 0, 200, "Altezza z"); Check(ct, 1, 5, "Coefficiente topografico ct");
        var p = Categories.SingleOrDefault(c => c.Category == category) ?? throw new ArgumentException("Categoria di esposizione non valida.");
        double log = Math.Log(Math.Max(z, p.Zmin) / p.Z0);
        return p.Kr * p.Kr * ct * log * (7 + ct * log);
    }
    /// <summary>Intervalli del modulo: fuori da questi limiti occorre un'analisi specifica.</summary>
    public static void Check(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max) throw new ArgumentException($"{name}: valore fuori intervallo [{min}, {max}].");
    }
}
