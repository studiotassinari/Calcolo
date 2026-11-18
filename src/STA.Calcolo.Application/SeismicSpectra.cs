using System.Globalization;
using System.IO.Compression;
using System.Text;
using STA.Carichi.Sisma;
namespace STA.Calcolo.Application;

/// <summary>Categorie topografiche NTC 2018 Tab. 3.2.III; ST in sommità secondo Tab. 3.2.V.</summary>
public enum TopographyCategory { T1, T2, T3, T4 }

/// <summary>
/// Dati del sito e dell'opera per gli spettri (unità: gradi WGS84, anni, %; q adimensionale).
/// Damping ξ in percentuale (5 % = spettri standard). Topografia: ST al colmo del rilievo (cautelativo).
/// </summary>
public sealed record SeismicSiteInput(double Longitude, double Latitude, int NominalLife, string UseClass, TipiTerreni Soil,
    TopographyCategory Topography = TopographyCategory.T1, double Q = 1.5, double Damping = 5);

/// <summary>
/// Parametri di uno stato limite. Le accelerazioni sono espresse in frazione di g (AgG, AmaxG) e, per ag, anche in m/s².
/// S = Ss·ST; amax = S·ag (accelerazione massima attesa al sito, usata ad esempio per i muri di sostegno).
/// </summary>
public sealed record SeismicLimitState(string Name, double Pvr, int Tr, double AgG, double Ag, double F0, double TcStar,
    double Ss, double Cc, double St, double S, double AmaxG, double Tb, double Tc, double Td, double Fv)
{
    public double Amax => AmaxG * SeismicSpectra.G;
}

/// <summary>Curva spettrale: periodo T [s] e accelerazione spettrale in frazione di g (Se/g).</summary>
public sealed record SpectrumCurve(string Name, string LimitState, bool Vertical, bool Design, IReadOnlyList<PlotPoint> Points);

public sealed record SeismicSiteResult(SeismicSiteInput Input, double ReferencePeriod, double Eta,
    IReadOnlyList<SeismicLimitState> States, IReadOnlyList<SpectrumCurve> Curves)
{
    public SeismicLimitState State(string name) => States.Single(s => s.Name == name);
}

/// <summary>
/// Spettri di risposta NTC 2018 §3.2.3. ag, F0 e Tc* provengono dal reticolo e dall'interpolazione del programma storico
/// (STA.Carichi.Sisma); le forme spettrali sono calcolate qui:
/// orizzontale (3.2.2) con S = Ss·ST e η = √(10/(5+ξ)) ≥ 0,55; verticale (3.2.8–3.2.10) con Ss = 1, TB = 0,05 s, TC = 0,15 s, TD = 1 s,
/// Fv = 1,35·F0·(ag/g)^0,5; progetto (§3.2.3.5) con η = 1/q e limite inferiore Sd ≥ 0,2·ag.
/// Per ST = 1 e ξ = 5 % l'orizzontale coincide con il motore storico (verificato dai test).
/// </summary>
public static class SeismicSpectra
{
    public const double G = 9.81;
    public static readonly string[] LimitStates = ["SLO", "SLD", "SLV", "SLC"];

    public static double TopographyFactor(TopographyCategory category) => category switch
    {
        TopographyCategory.T1 => 1.0, TopographyCategory.T2 => 1.2, TopographyCategory.T3 => 1.2, _ => 1.4
    };

    /// <summary>Coefficiente di correzione dello smorzamento (NTC 2018 eq. 3.2.4).</summary>
    public static double Eta(double damping) => Math.Max(.55, Math.Sqrt(10 / (5 + damping)));

    public static void Validate(SeismicSiteInput input)
    {
        void Require(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
        Require(double.IsFinite(input.Longitude) && input.Longitude is >= 6 and <= 19, "Longitudine fuori dal reticolo (6–19°).");
        Require(double.IsFinite(input.Latitude) && input.Latitude is >= 36 and <= 48, "Latitudine fuori dal reticolo (36–48°).");
        Require(input.NominalLife is 10 or 50 or 100, "Vita nominale ammessa: 10, 50 o 100 anni.");
        Require(input.UseClass is "I" or "II" or "III" or "IV", "Classe d'uso ammessa: I, II, III o IV.");
        Require(double.IsFinite(input.Q) && input.Q is >= 1 and <= 10, "Il fattore di struttura q deve essere compreso fra 1 e 10.");
        Require(double.IsFinite(input.Damping) && input.Damping is > 0 and <= 30, "Lo smorzamento ξ deve essere compreso fra 0 e 30 %.");
    }

    public static SeismicSiteResult Calculate(SeismicSiteInput input, double maxPeriod = 4, int samples = 401)
    {
        Validate(input);
        if (!(maxPeriod > 0.1) || maxPeriod > 20) throw new ArgumentException("Il periodo massimo deve essere compreso fra 0,1 e 20 s.");
        var m = new Sisma(input.Longitude, input.Latitude);
        m.TipoCostruzione = m.ElencoViteNominali.Single(v => v.Vn == input.NominalLife);
        m.ClasseUso = m.ElencoClassiUso.Single(v => v.ID == input.UseClass);
        m.CategoriaSottosuolo = m.ElencoCategorieSottosuolo.Single(v => v.Categoria == input.Soil);
        m.FattoreStruttura = 1; m.creaSpettri();
        var st = TopographyFactor(input.Topography); var eta = Eta(input.Damping);
        var states = m.spettri.Select(s =>
        {
            var agG = s.numAgMedia / G; var tc = s.numTc;
            return new SeismicLimitState(s.statoLimite.ToString(), s.numPvr, s.numTr, agG, s.numAgMedia, s.numF0Media, s.numTcStarMedia,
                s.numSs, s.numcC, st, s.numSs * st, s.numSs * st * agG, tc / 3, tc, 4 * agG + 1.6, 1.35 * s.numF0Media * Math.Sqrt(agG));
        }).ToArray();
        if (states.Any(s => !double.IsFinite(s.AgG) || !double.IsFinite(s.F0) || !double.IsFinite(s.TcStar)))
            throw new ArgumentException("Coordinate non coperte dal reticolo storico.");
        // Campionamento uniforme più i punti d'angolo, così il grafico e il cursore seguono esattamente le discontinuità di pendenza.
        double[] Periods(params double[] corners) => Enumerable.Range(0, samples).Select(i => maxPeriod * i / (samples - 1))
            .Concat(corners.Where(t => t > 0 && t < maxPeriod)).Distinct().Order().ToArray();
        var curves = new List<SpectrumCurve>();
        foreach (var s in states)
        {
            curves.Add(new($"{s.Name} H", s.Name, false, false, Periods(s.Tb, s.Tc, s.Td).Select(t => new PlotPoint(t, Horizontal(s, t, eta))).ToArray()));
            curves.Add(new($"{s.Name} V", s.Name, true, false, Periods(.05, .15, 1).Select(t => new PlotPoint(t, Vertical(s, t, eta))).ToArray()));
        }
        var slv = states.Single(s => s.Name == "SLV");
        curves.Add(new("SLV progetto H", "SLV", false, true, Periods(slv.Tb, slv.Tc, slv.Td).Select(t => new PlotPoint(t, Design(slv, t, input.Q))).ToArray()));
        return new(input, m.PeriodoRiferimento, eta, states, curves);
    }

    /// <summary>Spettro elastico orizzontale Se(T)/g (NTC 2018 eq. 3.2.2).</summary>
    public static double Horizontal(SeismicLimitState s, double t, double eta)
    {
        var a = s.AgG * s.S * eta * s.F0;
        if (t < s.Tb) return a * (t / s.Tb + 1 / (eta * s.F0) * (1 - t / s.Tb));
        if (t < s.Tc) return a;
        if (t < s.Td) return a * s.Tc / t;
        return a * s.Tc * s.Td / (t * t);
    }

    /// <summary>Spettro elastico verticale Sve(T)/g (NTC 2018 eq. 3.2.10, Tab. 3.2.VII: Ss = 1, TB = 0,05 s, TC = 0,15 s, TD = 1,0 s).</summary>
    public static double Vertical(SeismicLimitState s, double t, double eta)
    {
        double tb = .05, tc = .15, td = 1, a = s.AgG * s.St * eta * s.Fv;
        if (t < tb) return a * (t / tb + 1 / (eta * s.Fv) * (1 - t / tb));
        if (t < tc) return a;
        if (t < td) return a * tc / t;
        return a * tc * td / (t * t);
    }

    /// <summary>Spettro di progetto orizzontale Sd(T)/g (§3.2.3.5): elastico con η = 1/q, non inferiore a 0,2·ag/g.</summary>
    public static double Design(SeismicLimitState s, double t, double q) => Math.Max(Horizontal(s, t, 1 / q), .2 * s.AgG);

    /// <summary>Interpolazione lineare di una curva al periodo T (usata dal cursore del grafico e dagli altri moduli).</summary>
    public static double At(IReadOnlyList<PlotPoint> points, double t)
    {
        if (points.Count == 0) return double.NaN;
        if (t <= points[0].X) return points[0].Y;
        for (var i = 1; i < points.Count; i++)
            if (t <= points[i].X) { var a = points[i - 1]; var b = points[i]; return b.X == a.X ? b.Y : a.Y + (b.Y - a.Y) * (t - a.X) / (b.X - a.X); }
        return points[^1].Y;
    }

    /// <summary>ZIP con un file TXT per curva: T [s], Se/g, Se [m/s²]; punto decimale, intestazioni commentate con #.</summary>
    public static byte[] Zip(SeismicSiteResult r, string projectJson)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string text) { using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(text); }
            var inv = CultureInfo.InvariantCulture;
            foreach (var c in r.Curves)
            {
                var s = r.State(c.LimitState);
                var sb = new StringBuilder($"# {c.Name} · lon {r.Input.Longitude.ToString("0.000000", inv)} lat {r.Input.Latitude.ToString("0.000000", inv)}\n# ag/g = {s.AgG.ToString("0.0000", inv)} F0 = {s.F0.ToString("0.000", inv)} Tc* = {s.TcStar.ToString("0.000", inv)} S = {s.S.ToString("0.000", inv)}\n# T [s]\tSe/g\tSe [m/s2]\n");
                foreach (var p in c.Points) sb.Append($"{p.X.ToString("0.000", inv)}\t{p.Y.ToString("0.00000", inv)}\t{(p.Y * G).ToString("0.0000", inv)}\n");
                Entry(c.Name.Replace(' ', '-') + ".txt", sb.ToString());
            }
            Entry("progetto.json", projectJson);
            Entry("LEGGIMI.txt", "Spettri NTC 2018 §3.2.3 con parametri ag, F0, Tc* del reticolo storico. Colonne separate da TAB: periodo, accelerazione in g e in m/s2. Il progetto SLV usa eta = 1/q con limite 0,2 ag.\n");
        }
        return memory.ToArray();
    }
}

/// <summary>
/// Valori sismici restituiti a un modulo chiamante (ad esempio il muro di sostegno): dati del sito, stato limite scelto
/// con ag/g, F0, Tc*, S e amax/g, e nome del luogo se trovato con la ricerca.
/// </summary>
public sealed record SeismicSiteSelection(SeismicSiteInput Input, SeismicLimitState State, string Place);
