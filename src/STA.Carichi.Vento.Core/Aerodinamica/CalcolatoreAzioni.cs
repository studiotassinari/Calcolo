using System.Globalization;

namespace Vento.Aerodinamica
{
    /// <summary>
    /// Vento del sito necessario per passare dai coefficienti alle pressioni.
    /// </summary>
    /// <param name="Qr">Pressione cinetica di riferimento qr [N/m²]</param>
    /// <param name="Categoria">Categoria di esposizione (I ÷ V, Tab. 3.3.II NTC 2018)</param>
    /// <param name="Ct">Coefficiente di topografia ct [-]</param>
    /// <param name="Cd">Coefficiente dinamico cd [-] (§ 3.3.9 NTC 2018)</param>
    public sealed record DatiVentoSito(double Qr, string Categoria, double Ct, double Cd);

    /// <summary>
    /// Azione del vento su una superficie: coefficiente e pressioni che ne derivano.
    /// Le pressioni sono in N/m², positive se dirette verso la superficie; la forza è in kN.
    /// </summary>
    /// <param name="Superficie">Superficie o zona a cui si applica</param>
    /// <param name="Condizione">Condizione di carico (valori negativi / positivi)</param>
    /// <param name="Simbolo">"cpe" per gli edifici, "cF" per le tettoie</param>
    /// <param name="Coefficiente">Valore del coefficiente aerodinamico [-]</param>
    /// <param name="Ze">Quota di riferimento ze [m]</param>
    /// <param name="Ce">Coefficiente di esposizione ce(ze) [-]</param>
    /// <param name="Pressione">p = qr · ce(ze) · c · cd [N/m²]</param>
    /// <param name="PressioneNetta1">pe − pi con il primo valore di cpi [N/m²]; null se non richiesta</param>
    /// <param name="PressioneNetta2">pe − pi con il secondo valore di cpi [N/m²]; null se non richiesta</param>
    /// <param name="Forza">F = p · L² sulla falda [kN]; null per gli edifici</param>
    /// <param name="Riferimento">Tabella o figura della Circolare</param>
    public sealed record AzioneSuperficie(
        string Superficie,
        string Condizione,
        string Simbolo,
        double Coefficiente,
        double Ze,
        double Ce,
        double Pressione,
        double? PressioneNetta1,
        double? PressioneNetta2,
        double? Forza,
        string Riferimento)
    {
        /// <summary>Parte della costruzione a cui appartiene la superficie</summary>
        public ParteCostruzione Parte { get; init; } = ParteCostruzione.Copertura;

        /// <summary>Tratti della copertura occupati dalla zona, dal bordo sopravento [m]; vuoto per le pareti</summary>
        public IReadOnlyList<TrattoSviluppo> Tratti { get; init; } = Array.Empty<TrattoSviluppo>();

        /// <summary>
        /// Estensione della zona in forma leggibile: ascissa orizzontale x e ascissa s sullo sviluppo,
        /// entrambe dal bordo sopravento. Vuota per le pareti e dove non è definita con esattezza.
        /// </summary>
        public string Estensione { get; init; } = "";
    }

    /// <summary>
    /// Esito completo: azioni per superficie, pressione interna adottata e avvertenze.
    /// </summary>
    public sealed class RisultatoAzioni
    {
        public List<AzioneSuperficie> Azioni { get; } = new List<AzioneSuperficie>();

        /// <summary>Valori di cpi adottati (nessuno, uno oppure due)</summary>
        public double[] Cpi { get; set; } = Array.Empty<double>();

        /// <summary>Quota di riferimento zi della pressione interna [m]</summary>
        public double QuotaInterna { get; set; }

        /// <summary>Pressione cinetica di picco alla sommità della costruzione [N/m²]</summary>
        public double PressionePiccoSommità { get; set; }

        /// <summary>Avvertenze sui limiti di applicabilità delle tabelle</summary>
        public List<string> Avvertenze { get; } = new List<string>();

        /// <summary>Valutazioni intermedie: rapporti geometrici, quote, campi applicati, letture e pressione di picco</summary>
        public List<Valutazione> Valutazioni { get; } = new List<Valutazione>();

        /// <summary>Falso se la geometria non consente il calcolo</summary>
        public bool Valido { get; set; } = true;

        /// <summary>
        /// Condizioni di carico alternative presenti (ad esempio "valori negativi" e "valori positivi").
        /// Vuoto se ogni superficie ha un solo valore.
        /// </summary>
        public IReadOnlyList<string> Condizioni => Azioni.Select(a => a.Condizione).Where(c => c != CondizioneUnica).Distinct().ToList();

        /// <summary>Azioni di una condizione di carico: quelle a valore unico più quelle della condizione indicata</summary>
        public IReadOnlyList<AzioneSuperficie> PerCondizione(string condizione)
        {
            return Azioni.Where(a => a.Condizione == CondizioneUnica || a.Condizione == condizione).ToList();
        }

        /// <summary>Testo della colonna Condizione per le superfici con un solo valore</summary>
        public const string CondizioneUnica = "-";
    }

    /// <summary>
    /// Trasforma i coefficienti aerodinamici in pressioni e forze.
    /// qp(z) = qr · ce(z), con ce da <see cref="WindProfile.Exposure"/> (costante sotto zmin);
    /// p = qp(ze) · c · cd; pressione interna pi = qp(zi) · cpi con zi pari alla quota di sommità
    /// (§ C3.3.8.5); per le tettoie F = p · L², con L² area della falda (§ C3.3.8.2).
    /// Il coefficiente dinamico non si applica alla pressione interna.
    /// </summary>
    public static class CalcolatoreAzioni
    {
        /// <summary>
        /// Calcola le azioni del vento sulla costruzione.
        /// </summary>
        /// <exception cref="ArgumentException">Categoria non valida oppure quota oltre il campo del profilo (200 m)</exception>
        public static RisultatoAzioni Calcola(DatiVentoSito vento, GeometriaCostruzione geometria,
            CasoPressioneInterna casoInterno, FacciaEdificio facciaDominante)
        {
            var esito = new RisultatoAzioni();
            RisultatoCoefficienti coefficienti = CalcolatoreCoefficienti.Calcola(geometria);
            esito.Avvertenze.AddRange(coefficienti.Avvertenze);
            esito.Valido = coefficienti.Valido;
            if (!coefficienti.Valido) return esito;

            double sommità = geometria.AltezzaSommità;
            esito.PressionePiccoSommità = PressionePicco(vento, sommità);
            esito.Valutazioni.AddRange(coefficienti.Valutazioni);
            var cultura = CultureInfo.CurrentCulture;
            esito.Valutazioni.Add(new Valutazione("Pressione cinetica di picco alla sommità", "qp = qr · ce(" + sommità.ToString("0.##", cultura) + " m) = " +
                vento.Qr.ToString("0.0", cultura) + " · " + WindProfile.Exposure(vento.Categoria, sommità, vento.Ct).ToString("0.000", cultura),
                esito.PressionePiccoSommità.ToString("#,0", cultura) + " N/m²", "categoria di esposizione " + vento.Categoria));

            // pressione interna: solo per gli edifici, riferita alla quota di sommità
            double pressioneInternaBase = 0;
            if (!geometria.ÈTettoia)
            {
                esito.Cpi = CoefficientiPressione.PressioneInterna(casoInterno, CalcolatoreCoefficienti.CpeFaccia(geometria, facciaDominante));
                esito.QuotaInterna = sommità;
                pressioneInternaBase = esito.PressionePiccoSommità;
            }

            foreach (CoefficienteSuperficie c in coefficienti.Coefficienti)
            {
                bool forza = c.Tipo == TipoCoefficiente.Forza;
                double ce = WindProfile.Exposure(vento.Categoria, c.QuotaRiferimento, vento.Ct);
                double pressione = vento.Qr * ce * c.Valore * vento.Cd;

                esito.Azioni.Add(new AzioneSuperficie(
                    c.Superficie,
                    c.Condizione,
                    forza ? "cF" : "cpe",
                    c.Valore,
                    c.QuotaRiferimento,
                    ce,
                    pressione,
                    esito.Cpi.Length > 0 ? pressione - pressioneInternaBase * esito.Cpi[0] : null,
                    esito.Cpi.Length > 1 ? pressione - pressioneInternaBase * esito.Cpi[1] : null,
                    forza ? pressione * c.AreaRiferimento / 1000 : null,
                    c.Riferimento)
                {
                    Parte = c.Parte, Tratti = c.Tratti, Estensione = c.EstensioneEsatta ? DescriviEstensione(c.Tratti) : ""
                });
            }

            return esito;
        }

        /// <summary>Pressione cinetica di picco qp(z) = qr · ce(z) [N/m²]</summary>
        public static double PressionePicco(DatiVentoSito vento, double z)
        {
            return vento.Qr * WindProfile.Exposure(vento.Categoria, z, vento.Ct);
        }

        /// <summary>
        /// Descrive i tratti di una zona, ad esempio "x = 0,00 ÷ 18,31 m (s = 0,00 ÷ 21,64 m)".
        /// Lo sviluppo s è indicato solo quando differisce dall'ascissa orizzontale; più tratti sono separati da ";".
        /// </summary>
        public static string DescriviEstensione(IReadOnlyList<TrattoSviluppo> tratti)
        {
            if (tratti == null || tratti.Count == 0) return "";
            var cultura = CultureInfo.CurrentCulture;
            string Intervallo(double a, double b) => a.ToString("0.00", cultura) + " ÷ " + b.ToString("0.00", cultura);
            bool inclinato = tratti.Any(t => Math.Abs(t.SInizio - t.XInizio) > 5e-3 || Math.Abs(t.SFine - t.XFine) > 5e-3);
            string testo = "x = " + string.Join("; ", tratti.Select(t => Intervallo(t.XInizio, t.XFine))) + " m";
            if (inclinato) testo += " (s = " + string.Join("; ", tratti.Select(t => Intervallo(t.SInizio, t.SFine))) + " m)";
            return testo;
        }

        /// <summary>Testo del coefficiente con segno esplicito, nella cultura corrente (es. "+0,20")</summary>
        public static string ConSegno(double valore)
        {
            return valore.ToString("+0.00;-0.00", CultureInfo.CurrentCulture);
        }
    }
}
