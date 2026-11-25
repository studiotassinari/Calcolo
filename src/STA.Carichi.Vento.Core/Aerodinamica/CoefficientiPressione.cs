using System;

namespace Vento.Aerodinamica
{
    /// <summary>
    /// Formule dei coefficienti aerodinamici globali della Circolare 21/01/2019 n. 7 C.S.LL.PP., § C3.3.8.
    /// Funzioni pure: ogni metodo corrisponde a una tabella della Circolare.
    /// Gli angoli sono espressi in gradi; sono positive le pressioni dirette verso l'interno.
    /// </summary>
    public static class CoefficientiPressione
    {
        #region Pareti verticali - Tabella C3.3.I

        /// <summary>Faccia sopravento</summary>
        public static double PareteSopravento(double hSuD)
        {
            return hSuD <= 1 ? 0.7 + 0.1 * hSuD : 0.8;
        }

        /// <summary>Facce laterali</summary>
        public static double PareteLaterale(double hSuD)
        {
            return hSuD <= 0.5 ? -0.5 - 0.8 * hSuD : -0.9;
        }

        /// <summary>Faccia sottovento (valida fino a h/d = 5)</summary>
        public static double PareteSottovento(double hSuD)
        {
            return hSuD <= 1 ? -0.3 - 0.2 * hSuD : -0.5 - 0.05 * (Math.Min(hSuD, 5) - 1);
        }

        #endregion

        #region Coperture piane - Tabella C3.3.III

        /// <summary>Fascia sopravento di profondità min(b/2; h)</summary>
        public const double CoperturaPianaFasciaSopravento = -0.80;

        /// <summary>Restanti zone: da considerare con entrambi i segni</summary>
        public const double CoperturaPianaRestantiZone = 0.20;

        #endregion

        #region Coperture a falda singola - Tabelle C3.3.V e C3.3.VI

        /// <summary>
        /// Vento perpendicolare al colmo, valori negativi (Tabella C3.3.V).
        /// Restituisce null per α maggiore di 45°, dove esiste il solo valore positivo.
        /// </summary>
        public static double? FaldaSingolaNegativo(double alfa)
        {
            if (alfa <= -60) return -0.5;
            if (alfa <= -15) return -0.5 - (alfa + 60) / 90;
            if (alfa <= 30) return -1.0 + (alfa + 15) / 75;
            if (alfa <= 45) return -0.4 + (alfa - 30) / 37.5;
            return null;
        }

        /// <summary>
        /// Vento perpendicolare al colmo, valori positivi (Tabella C3.3.V).
        /// Restituisce null per α negativo, dove esiste il solo valore negativo.
        /// </summary>
        public static double? FaldaSingolaPositivo(double alfa)
        {
            if (alfa < 0) return null;
            if (alfa <= 45) return alfa / 75;
            return 0.6 + (Math.Min(alfa, 75) - 45) / 150;
        }

        /// <summary>Vento parallelo al colmo, fascia sopravento di profondità min(b/2; h) (Tabella C3.3.VI)</summary>
        public static double FaldaSingolaParallelaFasciaSopravento(double alfa)
        {
            double a = Math.Abs(alfa);
            return a <= 15 ? -0.8 - a / 50 : -1.10;
        }

        /// <summary>Vento parallelo al colmo, restanti zone (Tabella C3.3.VI)</summary>
        public static double FaldaSingolaParallelaRestantiZone(double alfa)
        {
            double a = Math.Abs(alfa);
            if (a <= 15) return -0.2 - a / 30;
            if (a <= 45) return -0.7 - (a - 15) / 150;
            return -0.9 + (a - 45) / 75;
        }

        #endregion

        #region Coperture a falda doppia - Tabelle C3.3.IX e C3.3.X

        /// <summary>Vento perpendicolare al colmo, falda sottovento (Tabella C3.3.IX)</summary>
        public static double FaldaDoppiaSottovento(double alfa)
        {
            if (alfa <= -15) return -0.85 + (Math.Max(alfa, -75) + 60) / 180;
            if (alfa <= 15) return -0.6;
            if (alfa <= 45) return -0.6 + (alfa - 15) / 100;
            return -0.3;
        }

        /// <summary>Vento parallelo al colmo, fascia sopravento di profondità min(b/2; h) (Tabella C3.3.X)</summary>
        public static double FaldaDoppiaParallelaFasciaSopravento(double alfa)
        {
            if (alfa <= -30) return -1.0;
            if (alfa <= 0) return -0.8 + alfa / 150;
            if (alfa <= 30) return -0.8 - alfa / 150;
            return -1.0;
        }

        /// <summary>Vento parallelo al colmo, restanti zone (Tabella C3.3.X)</summary>
        public static double FaldaDoppiaParallelaRestantiZone(double alfa)
        {
            if (alfa <= -30) return -0.9;
            if (alfa <= 10) return -0.9 + (alfa + 30) / 100;
            return -0.5;
        }

        #endregion

        #region Coperture a padiglione - Tabella C3.3.XIII

        /// <summary>Falde laterali, relative alle pareti parallele alla direzione del vento</summary>
        public static double PadiglioneFaldeLaterali(double alfa)
        {
            double a = Math.Abs(alfa);
            if (a <= 30) return -0.6 - a / 75;
            if (a <= 45) return -1.0;
            if (a <= 60) return -1.0 + (a - 45) / 37.5;
            return -0.6;
        }

        #endregion

        #region Coperture a volta cilindrica - Figura C3.3.19 (c)

        /// <summary>
        /// Zona A (sopravento). Restituisce i valori da considerare: uno solo oppure due
        /// (negativo e positivo) nel campo 0,2 ≤ f/d ≤ 0,3 con h/d > 0.
        /// Per 0 &lt; h/d &lt; 0,5 si interpola linearmente fra le curve h/d = 0 e h/d ≥ 0,5.
        /// </summary>
        public static double[] VoltaZonaA(double hSuD, double fSuD)
        {
            // curva h/d = 0
            double basso = Math.Min(1.6 * fSuD, 0.8);

            // peso della curva h/d >= 0,5
            double k = Math.Clamp(hSuD / 0.5, 0, 1);
            if (k == 0) return new[] { basso };

            // curve h/d >= 0,5
            double? negativo = null;
            double? positivo = null;
            if (fSuD <= 0.1) negativo = -0.7 - 5 * fSuD;
            else if (fSuD <= 0.2) negativo = -1.2;
            else if (fSuD <= 0.3) negativo = -1.2 + 9 * (fSuD - 0.2);
            if (fSuD >= 0.2) positivo = Math.Min(0.8 * (fSuD - 0.2) / 0.3, 0.8);

            if (negativo.HasValue && positivo.HasValue)
            {
                return new[]
                {
                    basso + k * (negativo.Value - basso),
                    basso + k * (positivo.Value - basso)
                };
            }

            double alto = negativo ?? positivo ?? basso;
            return new[] { basso + k * (alto - basso) };
        }

        /// <summary>Zone B (intermedie)</summary>
        public static double VoltaZonaB(double fSuD)
        {
            return Math.Max(-0.7 - fSuD, -1.2);
        }

        /// <summary>Zona C (sottovento)</summary>
        public static double VoltaZonaC(double fSuD)
        {
            return Math.Min(-0.7 + 3 * fSuD, -0.4);
        }

        #endregion

        #region Tettoie - Tabelle C3.3.XV, C3.3.XVI e C3.3.XVII

        /// <summary>Tettoia a falda singola, valore positivo del coefficiente di forza (tutti i φ)</summary>
        public static double TettoiaSingolaPositivo(double alfa)
        {
            return 0.2 + Math.Abs(alfa) / 30;
        }

        /// <summary>Tettoia a falda singola, valore negativo del coefficiente di forza</summary>
        public static double TettoiaSingolaNegativo(double alfa, double bloccaggio)
        {
            double libera = -0.5 - 1.3 * Math.Abs(alfa) / 30;
            return Interpola(libera, -1.4, bloccaggio);
        }

        /// <summary>Tettoia a falda doppia, valore positivo del coefficiente di forza (tutti i φ)</summary>
        public static double TettoiaDoppiaPositivo(double alfa)
        {
            return 0.2 + 0.7 * Math.Abs(alfa) / 30;
        }

        /// <summary>Tettoia a falda doppia, valore negativo del coefficiente di forza</summary>
        public static double TettoiaDoppiaNegativo(double alfa, double bloccaggio)
        {
            double libera = alfa <= 0 ? -0.5 + 0.1 * alfa / 10 : -0.5 - 0.2 * alfa / 10;
            return Interpola(libera, -1.4, bloccaggio);
        }

        /// <summary>
        /// Fattore riduttivo per tettoie a falde multiple (Tabella C3.3.XVII)
        /// </summary>
        /// <param name="campoDalBordo">1 = primo campo dal bordo, 2 = secondo campo, 3 o più = altri campi</param>
        /// <param name="coefficientePositivo">vero per cF &gt; 0</param>
        public static double TettoiaMultiplaFattoreRiduttivo(int campoDalBordo, bool coefficientePositivo)
        {
            if (campoDalBordo <= 1) return coefficientePositivo ? 1.0 : 0.8;
            if (campoDalBordo == 2) return coefficientePositivo ? 0.9 : 0.7;
            return 0.7;
        }

        #endregion

        #region Pressioni interne - § C3.3.8.5

        /// <summary>
        /// Coefficienti di pressione interna cpi da considerare
        /// </summary>
        /// <param name="caso">Caso di pressione interna</param>
        /// <param name="cpeSuperficieDominante">cpe della superficie con apertura dominante (solo caso 2)</param>
        public static double[] PressioneInterna(CasoPressioneInterna caso, double cpeSuperficieDominante)
        {
            switch (caso)
            {
                case CasoPressioneInterna.PorositàUniforme:
                    return new[] { 0.2, -0.3 };
                case CasoPressioneInterna.AperturaDominanteDoppia:
                    return new[] { 0.75 * cpeSuperficieDominante };
                case CasoPressioneInterna.AperturaDominanteTripla:
                    return new[] { 0.90 * cpeSuperficieDominante };
                default:
                    return Array.Empty<double>();
            }
        }

        #endregion

        /// <summary>
        /// Coefficiente locale per area di incidenza A compresa fra 1 e 10 m² - formula [C3.3.3]
        /// </summary>
        public static double CoefficienteLocale(double cpe1, double cpe10, double area)
        {
            if (area <= 1) return cpe1;
            if (area >= 10) return cpe10;
            return cpe1 - (cpe1 - cpe10) * Math.Log10(area);
        }

        private static double Interpola(double valoreZero, double valoreUno, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return valoreZero + (valoreUno - valoreZero) * t;
        }
    }
}
