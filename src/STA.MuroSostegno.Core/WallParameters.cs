namespace STA.MuroSostegno.Core;
/// <summary>Input in metri, kN, kPa, gradi e secondi come nel programma sorgente.</summary>
public sealed record WallParameter(string Key, string Label, string Unit, double Default, string Group);
public static class WallParameters
{
    public static IReadOnlyList<WallParameter> All { get; } = [
        new("H1", "Altezza muro H1", "m", 6.00, "Geometria"),
        new("S1", "Spessore sommità S1", "m", 0.40, "Geometria"),
        new("S2", "Spessore base S2", "m", 0.60, "Geometria"),
        new("Ds1", "Spiombamento ant. Ds1", "m", 0.20, "Geometria"),
        new("H2", "Spessore fond. ant. H2", "m", 0.60, "Geometria"),
        new("H3", "Spessore fond. post. H3", "m", 0.60, "Geometria"),
        new("H4", "Altezza dente H4", "m", 0.50, "Geometria"),
        new("S3", "Spessore dente S3", "m", 0.50, "Geometria"),
        new("Ba", "Sporto anteriore Ba", "m", 1.20, "Geometria"),
        new("Bf", "Larghezza fond. Bf", "m", 4.50, "Geometria"),
        new("Hta", "Altezza terr. ant. Hta", "m", 1.00, "Geometria"),
        new("It", "Pendenza terr. ant. It", "%", 10.00, "Geometria"),
        new("Htp", "Altezza terr. post. Htp", "m", 6.00, "Geometria"),
        new("Itp", "Pendenza terr. post. Itp", "%", 10.00, "Geometria"),
        new("Xd", "Posizione dente Xd", "m", 4.00, "Geometria"),
        new("GammaMuro", "γ Muro", "kN/m³", 25.00, "Materiali e carichi"),
        new("Sovraccarico", "Sovraccarico", "kN/m²", 10.00, "Materiali e carichi"),
        new("GammaTerreno", "γ Terreno", "kN/m³", 19.00, "Terreno e falda"),
        new("FI_T", "φ' attrito terreno", "°", 30.00, "Terreno e falda"),
        new("C1k", "c'ₖ coesione", "kPa", 0.05, "Terreno e falda"),
        new("Delta_T_M", "δ muro-terreno", "°", 15.00, "Terreno e falda"),
        new("Qfp", "Quota falda post. Qfp", "m", 0.75, "Terreno e falda"),
        new("Qfa", "Quota falda ant. Qfa", "m", 0.00, "Terreno e falda"),
        new("Vs", "Vₛ onde di taglio", "m/s", 0.209, "Sisma"),
        new("PGA", "PGA", "Amax/g", 0.20, "Sisma"),
        new("Csi_H", "χ coeff. riduzione", "", 1.00, "Sisma"),
        new("Tm", "Tm periodo", "s", 0.30, "Sisma"),
        new("Psi_Sovr", "Ψ sovraccarico", "", 0.50, "Sisma")
    ];
    public static Dictionary<string,double> Defaults() => All.ToDictionary(p => p.Key, p => p.Default);
}
