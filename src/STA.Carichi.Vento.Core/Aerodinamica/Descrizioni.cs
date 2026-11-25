namespace Vento.Aerodinamica
{
    /// <summary>
    /// Testi leggibili delle enumerazioni, condivisi da interfaccia e relazione così che usino le stesse parole.
    /// </summary>
    public static class Descrizioni
    {
        public static string Di(TipologiaCostruzione valore) => valore switch
        {
            TipologiaCostruzione.CoperturaPiana => "Edificio con copertura piana",
            TipologiaCostruzione.CoperturaFaldaSingola => "Edificio con copertura a falda singola",
            TipologiaCostruzione.CoperturaFaldaDoppia => "Edificio con copertura a falda doppia",
            TipologiaCostruzione.CoperturaPadiglione => "Edificio con copertura a padiglione",
            TipologiaCostruzione.CoperturaFaldeMultiple => "Edificio con copertura a falde multiple",
            TipologiaCostruzione.CoperturaVoltaCilindrica => "Edificio con copertura a volta cilindrica",
            TipologiaCostruzione.TettoiaFaldaSingola => "Tettoia a falda singola",
            TipologiaCostruzione.TettoiaFaldaDoppia => "Tettoia a falda doppia",
            _ => "Tettoia a falde multiple"
        };

        public static string Di(DirezioneVento valore) => valore == DirezioneVento.PerpendicolareAlColmo
            ? "Perpendicolare al colmo / alle generatrici"
            : "Parallelo al colmo / alle generatrici";

        public static string Di(SchemaFaldeMultiple valore) => valore switch
        {
            SchemaFaldeMultiple.ShedInclinazioneNegativa => "(a) shed, lato alto sopravento",
            SchemaFaldeMultiple.ShedInclinazionePositiva => "(b) shed, lato basso sopravento",
            SchemaFaldeMultiple.FaldeDoppieConColmi => "(c) falde doppie con colmi",
            _ => "(d) falde doppie con impluvi"
        };

        public static string Di(CasoPressioneInterna valore) => valore switch
        {
            CasoPressioneInterna.NonConsiderata => "Non considerata",
            CasoPressioneInterna.PorositàUniforme => "Porosità uniforme: cpi = +0,2 / −0,3",
            CasoPressioneInterna.AperturaDominanteDoppia => "Apertura dominante ≥ 2 volte le altre: cpi = 0,75 cpe",
            _ => "Apertura dominante ≥ 3 volte le altre: cpi = 0,90 cpe"
        };

        public static string Di(FacciaEdificio valore) => valore switch
        {
            FacciaEdificio.Sopravento => "Apertura sulla faccia sopravento",
            FacciaEdificio.Laterale => "Apertura su una faccia laterale",
            _ => "Apertura sulla faccia sottovento"
        };

        /// <summary>Vero se la tipologia ha un colmo o delle generatrici, quindi la direzione del vento è un dato</summary>
        public static bool HaDirezione(TipologiaCostruzione tipologia) =>
            tipologia != TipologiaCostruzione.CoperturaPiana && tipologia != TipologiaCostruzione.CoperturaPadiglione;

        /// <summary>Vero se la tipologia ha falde inclinate, quindi α è un dato</summary>
        public static bool HaInclinazione(TipologiaCostruzione tipologia) =>
            tipologia != TipologiaCostruzione.CoperturaPiana && tipologia != TipologiaCostruzione.CoperturaVoltaCilindrica;

        /// <summary>Vero se la tipologia è a falde multiple, quindi il numero di tratti è un dato</summary>
        public static bool HaTratti(TipologiaCostruzione tipologia) =>
            tipologia == TipologiaCostruzione.CoperturaFaldeMultiple || tipologia == TipologiaCostruzione.TettoiaFaldeMultiple;
    }
}
