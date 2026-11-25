using System.Globalization;
using Vento.Aerodinamica;
namespace STA.Calcolo.Reporting;

/// <summary>
/// Dati della relazione del vento sulle costruzioni. Le figure sono PNG prodotti dall'applicazione:
/// lo schema quotato e una figura delle pressioni per ogni condizione di carico.
/// </summary>
/// <param name="SiteLines">Dati del vento del sito come coppie etichetta/valore, nell'ordine della finestra</param>
/// <param name="Velocity">Velocità di riferimento [m/s]</param>
/// <param name="Wind">Pressione di riferimento, categoria, ct e cd</param>
/// <param name="Geometry">Geometria della costruzione</param>
/// <param name="InternalCase">Caso di pressione interna</param>
/// <param name="DominantFace">Faccia con apertura dominante (significativa solo nel caso 2)</param>
/// <param name="Actions">Azioni calcolate</param>
/// <param name="Scheme">Schema quotato della costruzione</param>
/// <param name="Pressures">Figure delle pressioni: condizione di carico e immagine</param>
public sealed record WindBuildingReportData(
    IReadOnlyList<(string Label, string Value)> SiteLines, double Velocity, DatiVentoSito Wind, GeometriaCostruzione Geometry,
    CasoPressioneInterna InternalCase, FacciaEdificio DominantFace, RisultatoAzioni Actions,
    byte[] Scheme, IReadOnlyList<(string Condition, byte[] Png)> Pressures);

/// <summary>
/// Capitolo "Azione del vento" (Circolare 21/01/2019 n. 7, § C3.3.8): metodo, vento del sito, geometria,
/// coefficienti e pressioni per superficie con l'estensione di ogni zona, figure e limiti.
/// Non ricalcola nulla: impagina i risultati ricevuti. Pressioni in N/m², lunghezze in m, forze in kN.
/// </summary>
public static class WindBuildingReportBuilder
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string N(double value, string format = "0.##") => value.ToString(format, It);
    private static string Signed(double value, string format = "+0.00;-0.00;0.00") => value.ToString(format, It).Replace('-', '−');
    private static string Pressure(double value) => value.ToString("+#,0;-#,0;0", It).Replace('-', '−');

    public static ReportChapter Build(WindBuildingReportData data)
    {
        var g = data.Geometry; var a = data.Actions; var blocks = new List<ChapterBlock>();
        bool canopy = g.ÈTettoia, vault = g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica;
        var category = Vento.WindProfile.Categories.Single(c => c.Category == data.Wind.Categoria);

        blocks.Add(new ChapterHeading(2, "Normativa e metodo"));
        blocks.Add(new ChapterParagraph("L'azione del vento è valutata secondo il D.M. 17/01/2018 (NTC 2018), § 3.3, con i coefficienti aerodinamici globali della Circolare 21/01/2019 n. 7 C.S.LL.PP., § C3.3.8. La pressione su ciascuna superficie è:"));
        blocks.Add(new ChapterFormula("p = q_{r} · c_{e}(z_{e}) · c · c_{d}",
            "dove qr è la pressione cinetica di riferimento, ce(ze) il coefficiente di esposizione alla quota di riferimento ze della superficie, c il coefficiente aerodinamico (" +
            (canopy ? "coefficiente di forza cF per le tettoie" : "coefficiente di pressione esterna cpe") + ") e cd il coefficiente dinamico."));
        blocks.Add(new ChapterFormula("c_{e}(z) = k_{r}^{2} · c_{t} · ln(\\frac{z}{z_{0}}) · [7 + c_{t} · ln(\\frac{z}{z_{0}})]",
            "per z ≥ zmin; per z < zmin si assume ce(z) = ce(zmin). Le pressioni sono positive se dirette verso la superficie e negative in depressione."));

        blocks.Add(new ChapterHeading(2, "Vento del sito"));
        var site = data.SiteLines.Select(l => new[] { l.Label, l.Value }).ToList();
        site.Add(["Velocità di riferimento vb", N(data.Velocity, "0.00") + " m/s"]);
        site.Add(["Pressione cinetica di riferimento qr", N(data.Wind.Qr, "0.0") + " N/m²"]);
        site.Add(["Categoria di esposizione", data.Wind.Categoria]);
        site.Add(["kr · z0 · zmin", N(category.Kr) + " · " + N(category.Z0) + " m · " + N(category.Zmin) + " m"]);
        blocks.Add(new ChapterTable("Parametri del vento", ["Parametro", "Valore"], site, [3, 2], 2));

        blocks.Add(new ChapterHeading(2, "Geometria della costruzione"));
        var geometry = new List<string[]> { new[] { "Tipologia", Descrizioni.Di(g.Tipologia) } };
        if (g.Tipologia == TipologiaCostruzione.CoperturaFaldeMultiple) geometry.Add(["Tipo costruttivo (Fig. C3.3.18)", Descrizioni.Di(g.SchemaMultiplo)]);
        if (Descrizioni.HaDirezione(g.Tipologia)) geometry.Add(["Direzione del vento", Descrizioni.Di(g.Direzione)]);
        geometry.Add(["b, dimensione ortogonale al vento", N(g.B) + " m"]);
        geometry.Add(["d, dimensione parallela al vento", N(g.D) + " m"]);
        geometry.Add([vault ? "h, quota di imposta della volta" : "h, altezza massima della copertura", N(g.H) + " m"]);
        if (Descrizioni.HaInclinazione(g.Tipologia)) geometry.Add(["α, inclinazione delle falde", N(g.Alfa) + "°"]);
        if (vault)
        {
            double development = CalcolatoreCoefficienti.SviluppoVolta(g.D, g.Freccia, out double radius, out _);
            geometry.Add(["f, freccia della volta", N(g.Freccia) + " m"]);
            geometry.Add(["Raggio della volta R = (d²/4 + f²)/(2f)", N(radius, "0.00") + " m"]);
            geometry.Add(["Sviluppo della volta L", N(development, "0.00") + " m"]);
            geometry.Add(["Sviluppo di ciascuna delle quattro zone L/4", N(development / 4, "0.00") + " m"]);
        }
        if (Descrizioni.HaTratti(g.Tipologia)) geometry.Add(["n, numero di tratti", g.NumeroTratti.ToString(It)]);
        if (canopy) geometry.Add(["φ, grado di bloccaggio", N(g.GradoBloccaggio)]);
        if (!canopy) geometry.Add(["Pressione interna (§ C3.3.8.5)", Descrizioni.Di(data.InternalCase) +
            (data.InternalCase is CasoPressioneInterna.AperturaDominanteDoppia or CasoPressioneInterna.AperturaDominanteTripla ? " · " + Descrizioni.Di(data.DominantFace) : "")]);
        blocks.Add(new ChapterTable("Dati geometrici", ["Parametro", "Valore"], geometry, [3, 2], 2));
        blocks.Add(new ChapterFigure(data.Scheme, "Schema quotato: sezione nella direzione del vento e pianta. b è la dimensione ortogonale al vento, d quella parallela.", .95));

        blocks.Add(new ChapterHeading(2, "Coefficienti aerodinamici e pressioni"));
        blocks.Add(new ChapterParagraph($"Alla sommità della costruzione (z = {N(g.AltezzaSommità)} m) la pressione cinetica di picco vale qp = qr · ce = {N(a.PressionePiccoSommità, "#,0")} N/m². " +
            "Per ogni superficie la tabella riporta la quota di riferimento ze, il coefficiente di esposizione, il coefficiente aerodinamico con il riferimento alla Circolare e la pressione che ne deriva." +
            (a.Condizioni.Count > 0 ? " Dove la Circolare fornisce due valori, le due condizioni di carico vanno considerate entrambe." : "")));
        // Valutazioni intermedie: rapporti h/d e f/d, quote di riferimento, campi applicati, letture e interpolazioni.
        if (a.Valutazioni.Count > 0)
            blocks.Add(new ChapterTable("Valutazioni che determinano i coefficienti", ["Grandezza", "Come è ottenuta", "Valore", "Significato"],
                a.Valutazioni.Select(v => new[] { v.Grandezza, v.Espressione, v.Valore.Replace('-', '−'), v.Nota }).ToList(), [2.6, 2.8, 1.4, 3.6], 4));
        var headers = new List<string> { "Superficie", "Condizione", "ze [m]", "ce(ze)", canopy ? "cF" : "cpe", "p [N/m²]" };
        var widths = new List<double> { 3.2, 2, 1, 1, 1, 1.2 };
        if (a.Cpi.Length > 0) { headers.Add("p − pi (" + Signed(a.Cpi[0]) + ")"); widths.Add(1.5); }
        if (a.Cpi.Length > 1) { headers.Add("p − pi (" + Signed(a.Cpi[1]) + ")"); widths.Add(1.5); }
        if (canopy) { headers.Add("F [kN]"); widths.Add(1.2); }
        headers.Add("Rif."); widths.Add(1.8);
        var rows = a.Azioni.Select(r =>
        {
            var row = new List<string> { r.Superficie, r.Condizione, N(r.Ze, "0.00"), N(r.Ce, "0.000"), Signed(r.Coefficiente), Pressure(r.Pressione) };
            if (a.Cpi.Length > 0) row.Add(Pressure(r.PressioneNetta1 ?? 0));
            if (a.Cpi.Length > 1) row.Add(Pressure(r.PressioneNetta2 ?? 0));
            if (canopy) row.Add((r.Forza ?? 0).ToString("+0.00;-0.00;0.00", It).Replace('-', '−'));
            row.Add(r.Riferimento);
            return row.ToArray();
        }).ToList();
        blocks.Add(new ChapterTable("Coefficienti e pressioni per superficie", headers.ToArray(), rows, widths.ToArray(), 2));
        if (a.Cpi.Length > 0)
            blocks.Add(new ChapterParagraph($"Pressione interna: cpi = {string.Join(" e ", a.Cpi.Select(v => Signed(v)))}, riferita alla quota zi = {N(a.QuotaInterna)} m (sommità). " +
                "Le colonne p − pi riportano la pressione netta per ciascun valore di cpi; il coefficiente dinamico non si applica alla pressione interna."));
        if (canopy)
            blocks.Add(new ChapterParagraph("Per le tettoie F = p · L² è la forza risultante sulla falda, diretta ortogonalmente ad essa, con L² area della falda; è applicata a d/4 dal bordo sopravento della falda (Fig. C3.3.22 e C3.3.24)."));

        // Estensione delle zone lungo la direzione del vento: x in orizzontale, s misurata sullo sviluppo della copertura.
        var zones = a.Azioni.Where(r => r.Parte == ParteCostruzione.Copertura && r.Tratti.Count > 0 && r.Estensione.Length > 0)
            .GroupBy(r => r.Superficie).Select(group => group.First()).ToList();
        if (zones.Count > 0)
        {
            blocks.Add(new ChapterHeading(2, "Distribuzione longitudinale delle zone"));
            blocks.Add(new ChapterParagraph("Le zone della copertura si succedono nella direzione del vento a partire dal bordo sopravento. Per ogni zona sono indicati l'ascissa orizzontale x di inizio e fine, " +
                "l'ascissa s misurata sullo sviluppo della copertura e la lunghezza del tratto sullo sviluppo." +
                (vault ? " La volta è divisa in quattro zone di uguale sviluppo: A sopravento, due zone B intermedie e C sottovento (§ C3.3.8.1.7)." : "")));
            var zoneRows = zones.SelectMany(r => r.Tratti.Select(t => new[]
            {
                r.Superficie, N(t.XInizio, "0.00"), N(t.XFine, "0.00"), N(t.SInizio, "0.00"), N(t.SFine, "0.00"), N(t.Lunghezza, "0.00"),
                string.Join(" / ", a.Azioni.Where(o => o.Superficie == r.Superficie).Select(o => Pressure(o.Pressione)))
            })).ToList();
            blocks.Add(new ChapterTable("Estensione delle zone e pressioni", ["Zona", "x inizio [m]", "x fine [m]", "s inizio [m]", "s fine [m]", "Sviluppo [m]", "p [N/m²]"], zoneRows, [3.2, 1.2, 1.2, 1.2, 1.2, 1.2, 1.6]));
        }

        blocks.Add(new ChapterHeading(2, "Figura delle pressioni"));
        foreach (var (condition, png) in data.Pressures)
            blocks.Add(new ChapterFigure(png, "Pressioni sulla sezione nella direzione del vento, in N/m²" + (condition.Length > 0 ? " · condizione: " + condition : "") +
                ". Lo spessore delle fasce è proporzionale a |p|; in basso le ascisse x dei confini fra le zone."));

        blocks.Add(new ChapterHeading(2, "Avvertenze e limiti"));
        foreach (string warning in a.Avvertenze) blocks.Add(new ChapterBullet(warning));
        blocks.Add(new ChapterBullet("Sono considerati i coefficienti globali; le pressioni locali su elementi di piccola area (cpe,10 e cpe,1) richiedono una valutazione separata."));
        blocks.Add(new ChapterBullet("b è la dimensione ortogonale al vento e d quella parallela: per l'altra direzione del vento il calcolo va ripetuto scambiandole."));
        if (g.Tipologia == TipologiaCostruzione.CoperturaPadiglione)
            blocks.Add(new ChapterBullet("Per la copertura a padiglione l'estensione delle falde dipende dalla pianta e non è riportata; nella figura le falde sopravento e sottovento sono rappresentate in sezione di mezzeria."));
        if (vault) blocks.Add(new ChapterBullet("I coefficienti della volta cilindrica sono letti per punti dalla Fig. C3.3.19 (c) della Circolare, con interpolazione lineare."));

        return new("Azione del vento", blocks);
    }
}
