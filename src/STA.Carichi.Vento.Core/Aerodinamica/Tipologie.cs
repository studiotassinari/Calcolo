using System.Collections.Generic;

namespace Vento.Aerodinamica
{
    /// <summary>
    /// Tipologie di costruzione trattate dalla Circolare 21/01/2019 n. 7, § C3.3.8.1 e § C3.3.8.2
    /// </summary>
    public enum TipologiaCostruzione
    {
        /// <summary>Edificio a pianta rettangolare con copertura piana (§ C3.3.8.1.2)</summary>
        CoperturaPiana,

        /// <summary>Edificio a pianta rettangolare con copertura a falda singola (§ C3.3.8.1.3)</summary>
        CoperturaFaldaSingola,

        /// <summary>Edificio a pianta rettangolare con copertura a falda doppia (§ C3.3.8.1.4)</summary>
        CoperturaFaldaDoppia,

        /// <summary>Edificio a pianta rettangolare con copertura a padiglione (§ C3.3.8.1.5)</summary>
        CoperturaPadiglione,

        /// <summary>Edificio a pianta rettangolare con copertura a falde multiple (§ C3.3.8.1.6)</summary>
        CoperturaFaldeMultiple,

        /// <summary>Edificio a pianta rettangolare con copertura a volta cilindrica (§ C3.3.8.1.7)</summary>
        CoperturaVoltaCilindrica,

        /// <summary>Tettoia a falda singola (§ C3.3.8.2.1)</summary>
        TettoiaFaldaSingola,

        /// <summary>Tettoia a falda doppia (§ C3.3.8.2.2)</summary>
        TettoiaFaldaDoppia,

        /// <summary>Tettoia a falde multiple (§ C3.3.8.2.3)</summary>
        TettoiaFaldeMultiple
    }

    /// <summary>
    /// Direzione del vento rispetto alla linea di colmo (o alle generatrici della volta)
    /// </summary>
    public enum DirezioneVento
    {
        /// <summary>Vento ortogonale al colmo</summary>
        PerpendicolareAlColmo,

        /// <summary>Vento parallelo al colmo</summary>
        ParalleloAlColmo
    }

    /// <summary>
    /// Tipi costruttivi delle coperture a falde multiple (Figura C3.3.18)
    /// </summary>
    public enum SchemaFaldeMultiple
    {
        /// <summary>(a) shed con falde singole a inclinazione negativa (lato alto sopravento)</summary>
        ShedInclinazioneNegativa,

        /// <summary>(b) shed con falde singole a inclinazione positiva (lato basso sopravento)</summary>
        ShedInclinazionePositiva,

        /// <summary>(c) successione di falde doppie con colmi (prima falda sopravento ascendente)</summary>
        FaldeDoppieConColmi,

        /// <summary>(d) successione di falde doppie con impluvi (prima falda sopravento discendente)</summary>
        FaldeDoppieConImpluvi
    }

    /// <summary>
    /// Casi di pressione interna (§ C3.3.8.5)
    /// </summary>
    public enum CasoPressioneInterna
    {
        /// <summary>Pressione interna non considerata</summary>
        NonConsiderata,

        /// <summary>Caso 3: porosità distribuita uniformemente, cpi = +0,2 e cpi = -0,3</summary>
        PorositàUniforme,

        /// <summary>Caso 2: aperture su una superficie almeno doppie delle rimanenti, cpi = 0,75 cpe</summary>
        AperturaDominanteDoppia,

        /// <summary>Caso 2: aperture su una superficie almeno triple delle rimanenti, cpi = 0,90 cpe</summary>
        AperturaDominanteTripla
    }

    /// <summary>
    /// Faccia dell'edificio su cui si trova l'apertura dominante (caso 2 del § C3.3.8.5)
    /// </summary>
    public enum FacciaEdificio
    {
        Sopravento,
        Laterale,
        Sottovento
    }

    /// <summary>
    /// Natura del coefficiente restituito
    /// </summary>
    public enum TipoCoefficiente
    {
        /// <summary>Coefficiente di pressione esterna globale cpe</summary>
        PressioneEsterna,

        /// <summary>Coefficiente di forza cF (tettoie)</summary>
        Forza
    }

    /// <summary>
    /// Dati geometrici della costruzione. Le dimensioni b e d sono sempre riferite alla direzione del vento
    /// (b ortogonale al flusso, d parallela al flusso), come nelle figure della Circolare.
    /// </summary>
    public sealed class GeometriaCostruzione
    {
        public TipologiaCostruzione Tipologia { get; set; } = TipologiaCostruzione.CoperturaFaldaDoppia;

        public DirezioneVento Direzione { get; set; } = DirezioneVento.PerpendicolareAlColmo;

        /// <summary>Dimensione in pianta ortogonale al vento [m]</summary>
        public double B { get; set; }

        /// <summary>Dimensione in pianta parallela al vento [m]</summary>
        public double D { get; set; }

        /// <summary>
        /// Altezza [m]: quota massima della copertura; per la volta cilindrica è la quota di imposta
        /// </summary>
        public double H { get; set; }

        /// <summary>Inclinazione delle falde sull'orizzontale [°], con il segno delle figure della Circolare</summary>
        public double Alfa { get; set; }

        /// <summary>Freccia della volta cilindrica [m]</summary>
        public double Freccia { get; set; }

        /// <summary>Numero di tratti (campi) delle coperture e tettoie a falde multiple</summary>
        public int NumeroTratti { get; set; } = 3;

        public SchemaFaldeMultiple SchemaMultiplo { get; set; } = SchemaFaldeMultiple.FaldeDoppieConColmi;

        /// <summary>Grado di bloccaggio φ delle tettoie, compreso fra 0 e 1</summary>
        public double GradoBloccaggio { get; set; }

        /// <summary>Vero se la tipologia è una tettoia (coefficienti di forza)</summary>
        public bool ÈTettoia =>
            Tipologia == TipologiaCostruzione.TettoiaFaldaSingola ||
            Tipologia == TipologiaCostruzione.TettoiaFaldaDoppia ||
            Tipologia == TipologiaCostruzione.TettoiaFaldeMultiple;

        /// <summary>Quota di sommità della costruzione [m]</summary>
        public double AltezzaSommità =>
            Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica ? H + Freccia : H;
    }

    /// <summary>
    /// Coefficiente aerodinamico relativo a una superficie (o a una sua zona)
    /// </summary>
    /// <param name="Superficie">Superficie o zona a cui si applica</param>
    /// <param name="Condizione">Condizione di carico (es. valori negativi / positivi)</param>
    /// <param name="Valore">Valore del coefficiente (positivo se diretto verso l'interno / verso il basso)</param>
    /// <param name="QuotaRiferimento">Quota di riferimento ze [m]</param>
    /// <param name="Tipo">cpe oppure cF</param>
    /// <param name="AreaRiferimento">Area su cui agisce la forza risultante [m²] (solo tettoie, altrimenti 0)</param>
    /// <param name="Riferimento">Tabella o paragrafo della Circolare</param>
    public sealed record CoefficienteSuperficie(
        string Superficie,
        string Condizione,
        double Valore,
        double QuotaRiferimento,
        TipoCoefficiente Tipo,
        double AreaRiferimento,
        string Riferimento)
    {
        /// <summary>Parte della costruzione a cui appartiene la superficie</summary>
        public ParteCostruzione Parte { get; init; } = ParteCostruzione.Copertura;

        /// <summary>
        /// Tratti della copertura occupati dalla zona, misurati dal bordo sopravento nella direzione del vento.
        /// Vuoto per le pareti e per le falde non rappresentabili nella sezione (falde laterali del padiglione).
        /// </summary>
        public IReadOnlyList<TrattoSviluppo> Tratti { get; init; } = System.Array.Empty<TrattoSviluppo>();

        /// <summary>Falso se i tratti servono solo al disegno e non descrivono con esattezza la zona (padiglione)</summary>
        public bool EstensioneEsatta { get; init; } = true;
    }

    /// <summary>
    /// Passaggio intermedio del calcolo, mostrato all'utente e riportato nella relazione
    /// </summary>
    /// <param name="Grandezza">Nome della grandezza valutata</param>
    /// <param name="Espressione">Come è ottenuta (formula o lettura), con i numeri sostituiti</param>
    /// <param name="Valore">Risultato, con unità di misura</param>
    /// <param name="Nota">Significato per il calcolo: campo di validità, tabella o figura di riferimento</param>
    public sealed record Valutazione(string Grandezza, string Espressione, string Valore, string Nota);

    /// <summary>
    /// Parte della costruzione: serve a collocare l'azione nella figura dei risultati
    /// </summary>
    public enum ParteCostruzione
    {
        PareteSopravento,
        PareteLaterale,
        PareteSottovento,
        Copertura,
        FaldaLaterale
    }

    /// <summary>
    /// Tratto di copertura lungo la direzione del vento. Tutte le lunghezze sono in metri e partono dal bordo sopravento.
    /// </summary>
    /// <param name="XInizio">Ascissa orizzontale di inizio [m]</param>
    /// <param name="XFine">Ascissa orizzontale di fine [m]</param>
    /// <param name="SInizio">Ascissa di inizio misurata sullo sviluppo della copertura [m]</param>
    /// <param name="SFine">Ascissa di fine misurata sullo sviluppo della copertura [m]</param>
    public sealed record TrattoSviluppo(double XInizio, double XFine, double SInizio, double SFine)
    {
        /// <summary>Lunghezza del tratto misurata sullo sviluppo [m]</summary>
        public double Lunghezza => SFine - SInizio;
    }

    /// <summary>
    /// Esito del calcolo dei coefficienti aerodinamici
    /// </summary>
    public sealed class RisultatoCoefficienti
    {
        public List<CoefficienteSuperficie> Coefficienti { get; } = new List<CoefficienteSuperficie>();

        /// <summary>Avvertenze sui limiti di applicabilità</summary>
        public List<string> Avvertenze { get; } = new List<string>();

        /// <summary>
        /// Valutazioni intermedie che portano ai coefficienti: rapporti geometrici (h/d, f/d), quote di
        /// riferimento, campo della tabella o del grafico applicato, valori letti e interpolazioni.
        /// </summary>
        public List<Valutazione> Valutazioni { get; } = new List<Valutazione>();

        /// <summary>Vero se i dati in ingresso consentono il calcolo</summary>
        public bool Valido { get; set; } = true;
    }
}
