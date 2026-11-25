using System;
using System.Globalization;

namespace Vento.Aerodinamica
{
    /// <summary>
    /// Compone, per la tipologia di costruzione scelta, l'elenco dei coefficienti aerodinamici
    /// globali da applicare alle singole superfici (Circolare 21/01/2019 n. 7, § C3.3.8.1 e § C3.3.8.2).
    /// </summary>
    public static class CalcolatoreCoefficienti
    {
        private const string Negativi = "valori negativi";
        private const string Positivi = "valori positivi";
        private const string Unica = "-";

        /// <summary>
        /// Calcola i coefficienti aerodinamici della costruzione
        /// </summary>
        public static RisultatoCoefficienti Calcola(GeometriaCostruzione g)
        {
            var r = new RisultatoCoefficienti();

            if (!(g.B > 0) || !(g.D > 0) || !(g.H > 0))
            {
                r.Valido = false;
                r.Avvertenze.Add("Inserire valori positivi per b, d e h.");
                return r;
            }

            if (g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica && !(g.Freccia > 0))
            {
                r.Valido = false;
                r.Avvertenze.Add("Inserire un valore positivo per la freccia f della volta.");
                return r;
            }

            if (g.ÈTettoia)
            {
                CalcolaTettoia(g, r);
            }
            else
            {
                AggiungiPareti(g, r);
                CalcolaCopertura(g, r);
            }

            return r;
        }

        /// <summary>
        /// cpe della faccia indicata, usato per la pressione interna con apertura dominante
        /// </summary>
        public static double CpeFaccia(GeometriaCostruzione g, FacciaEdificio faccia)
        {
            if (!(g.D > 0)) return 0;
            double hSuD = g.AltezzaSommità / g.D;
            switch (faccia)
            {
                case FacciaEdificio.Sopravento: return CoefficientiPressione.PareteSopravento(hSuD);
                case FacciaEdificio.Laterale: return CoefficientiPressione.PareteLaterale(hSuD);
                default: return CoefficientiPressione.PareteSottovento(hSuD);
            }
        }

        #region pareti

        private static void AggiungiPareti(GeometriaCostruzione g, RisultatoCoefficienti r)
        {
            const string rif = "Tab. C3.3.I";
            double h = g.AltezzaSommità;
            double hSuD = h / g.D;
            double sopravento = CoefficientiPressione.PareteSopravento(hSuD);

            if (h <= g.B)
            {
                Cpe(r, "Parete sopravento", Unica, sopravento, h, rif);
            }
            else
            {
                // edificio alto: due zone con quote di riferimento distinte (§ C3.3.8.1.1.1, criterio 1)
                Cpe(r, "Parete sopravento, z ≤ b", Unica, sopravento, g.B, rif);
                Cpe(r, "Parete sopravento, b < z ≤ h", Unica, sopravento, h, rif);
            }

            Cpe(r, "Pareti laterali", Unica, CoefficientiPressione.PareteLaterale(hSuD), h, rif);
            Cpe(r, "Parete sottovento", Unica, CoefficientiPressione.PareteSottovento(hSuD), h, rif);

            if (hSuD > 5)
            {
                r.Avvertenze.Add("h/d > 5: edificio particolarmente snello, fuori dal campo della Tab. C3.3.I (servono studi specifici).");
            }
        }

        #endregion

        #region coperture

        private static void CalcolaCopertura(GeometriaCostruzione g, RisultatoCoefficienti r)
        {
            double ze = g.AltezzaSommità;
            bool perpendicolare = g.Direzione == DirezioneVento.PerpendicolareAlColmo;
            bool aFalde = g.Tipologia != TipologiaCostruzione.CoperturaPiana &&
                          g.Tipologia != TipologiaCostruzione.CoperturaVoltaCilindrica;

            if (aFalde && Math.Abs(g.Alfa) <= 5)
            {
                r.Avvertenze.Add("Inclinazione compresa fra -5° e +5°: si applicano i coefficienti delle coperture piane.");
                CoperturaPiana(g, r, ze);
                return;
            }

            if (aFalde && Math.Abs(g.Alfa) > 75)
            {
                r.Avvertenze.Add("Inclinazione oltre 75°: fuori dal campo delle tabelle; adottati i valori limite.");
            }

            switch (g.Tipologia)
            {
                case TipologiaCostruzione.CoperturaPiana:
                    CoperturaPiana(g, r, ze);
                    break;

                case TipologiaCostruzione.CoperturaFaldaSingola:
                    if (perpendicolare) FaldaSingola(r, "Falda", g.Alfa, ze, 1);
                    else FaldaSingolaParallela(g, r, g.Alfa, ze);
                    break;

                case TipologiaCostruzione.CoperturaFaldaDoppia:
                    if (perpendicolare)
                    {
                        FaldaSingola(r, "Falda sopravento", g.Alfa, ze, 1);
                        Cpe(r, "Falda sottovento", Unica, CoefficientiPressione.FaldaDoppiaSottovento(g.Alfa), ze, "Tab. C3.3.IX");
                    }
                    else
                    {
                        FaldaDoppiaParallela(g, r, g.Alfa, ze);
                    }
                    break;

                case TipologiaCostruzione.CoperturaPadiglione:
                    double a = Math.Abs(g.Alfa);
                    FaldaSingola(r, "Falda sopravento", a, ze, 1);
                    Cpe(r, "Falda sottovento", Unica, CoefficientiPressione.FaldaDoppiaSottovento(a), ze, "Tab. C3.3.IX");
                    Cpe(r, "Falde laterali", Unica, CoefficientiPressione.PadiglioneFaldeLaterali(a), ze, "Tab. C3.3.XIII");
                    break;

                case TipologiaCostruzione.CoperturaFaldeMultiple:
                    FaldeMultiple(g, r, ze, perpendicolare);
                    break;

                case TipologiaCostruzione.CoperturaVoltaCilindrica:
                    VoltaCilindrica(g, r, perpendicolare);
                    break;
            }
        }

        private static void CoperturaPiana(GeometriaCostruzione g, RisultatoCoefficienti r, double ze)
        {
            const string rif = "Tab. C3.3.III";
            Cpe(r, "Copertura, " + FasciaSopravento(g), Unica, CoefficientiPressione.CoperturaPianaFasciaSopravento, ze, rif);
            Cpe(r, "Copertura, restanti zone", Negativi, -CoefficientiPressione.CoperturaPianaRestantiZone, ze, rif);
            Cpe(r, "Copertura, restanti zone", Positivi, CoefficientiPressione.CoperturaPianaRestantiZone, ze, rif);
        }

        /// <summary>
        /// Falda trattata come falda singola con vento perpendicolare al colmo (Tab. C3.3.V),
        /// con eventuale fattore riduttivo delle falde multiple
        /// </summary>
        private static void FaldaSingola(RisultatoCoefficienti r, string nome, double alfa, double ze, double fattore)
        {
            const string rif = "Tab. C3.3.V";
            double? negativo = CoefficientiPressione.FaldaSingolaNegativo(alfa);
            double? positivo = CoefficientiPressione.FaldaSingolaPositivo(alfa);

            if (negativo.HasValue) Cpe(r, nome, positivo.HasValue ? Negativi : Unica, negativo.Value * fattore, ze, rif);
            if (positivo.HasValue) Cpe(r, nome, negativo.HasValue ? Positivi : Unica, positivo.Value * fattore, ze, rif);
        }

        private static void FaldaSingolaParallela(GeometriaCostruzione g, RisultatoCoefficienti r, double alfa, double ze)
        {
            const string rif = "Tab. C3.3.VI";
            Cpe(r, "Copertura, " + FasciaSopravento(g), Unica, CoefficientiPressione.FaldaSingolaParallelaFasciaSopravento(alfa), ze, rif);
            Cpe(r, "Copertura, restanti zone", Unica, CoefficientiPressione.FaldaSingolaParallelaRestantiZone(alfa), ze, rif);
        }

        private static void FaldaDoppiaParallela(GeometriaCostruzione g, RisultatoCoefficienti r, double alfa, double ze)
        {
            const string rif = "Tab. C3.3.X";
            Cpe(r, "Copertura, " + FasciaSopravento(g), Unica, CoefficientiPressione.FaldaDoppiaParallelaFasciaSopravento(alfa), ze, rif);
            Cpe(r, "Copertura, restanti zone", Unica, CoefficientiPressione.FaldaDoppiaParallelaRestantiZone(alfa), ze, rif);
        }

        /// <summary>
        /// Coperture a falde multiple (§ C3.3.8.1.6 e Figura C3.3.18)
        /// </summary>
        private static void FaldeMultiple(GeometriaCostruzione g, RisultatoCoefficienti r, double ze, bool perpendicolare)
        {
            double a = Math.Abs(g.Alfa);
            int tratti = Math.Max(g.NumeroTratti, 1);

            if (!perpendicolare)
            {
                // nessun fattore riduttivo: valgono i coefficienti della copertura corrispondente
                switch (g.SchemaMultiplo)
                {
                    case SchemaFaldeMultiple.FaldeDoppieConColmi: FaldaDoppiaParallela(g, r, a, ze); break;
                    case SchemaFaldeMultiple.FaldeDoppieConImpluvi: FaldaDoppiaParallela(g, r, -a, ze); break;
                    default: FaldaSingolaParallela(g, r, a, ze); break;
                }
                return;
            }

            // gruppi di tratti con lo stesso fattore riduttivo: 1°, 2°, dal 3° in poi
            int gruppi = Math.Min(tratti, 3);
            for (int i = 1; i <= gruppi; i++)
            {
                string tratto = i < 3 ? "Tratto " + i : (tratti > 3 ? "Tratti 3-" + tratti : "Tratto 3");
                double fattore = i == 1 ? 1.0 : (i == 2 ? 0.8 : 0.6);

                switch (g.SchemaMultiplo)
                {
                    case SchemaFaldeMultiple.ShedInclinazioneNegativa:
                        FaldaSingola(r, tratto + ", falda", -a, ze, fattore);
                        break;

                    case SchemaFaldeMultiple.ShedInclinazionePositiva:
                        double? negativo = CoefficientiPressione.FaldaSingolaNegativo(a);
                        double? positivo = CoefficientiPressione.FaldaSingolaPositivo(a);
                        if (negativo.HasValue)
                            Cpe(r, tratto + ", falda", Negativi, negativo.Value * fattore, ze, "Tab. C3.3.V - Fig. C3.3.18 (b)");
                        if (positivo.HasValue)
                            Cpe(r, tratto + ", falda", Positivi, i == 1 ? positivo.Value : -0.4, ze, "Tab. C3.3.V - Fig. C3.3.18 (b)");
                        break;

                    case SchemaFaldeMultiple.FaldeDoppieConColmi:
                        if (i == 1)
                        {
                            FaldaSingola(r, "Tratto 1, prima falda sopravento", a, ze, 1);
                        }
                        else
                        {
                            // tratti successivi: falda doppia con inclinazione negativa, ridotta di 0,6 dal terzo tratto
                            double f = i == 2 ? 1.0 : 0.6;
                            FaldaSingola(r, tratto + ", falda discendente", -a, ze, f);
                            Cpe(r, tratto + ", falda ascendente", Unica, CoefficientiPressione.FaldaDoppiaSottovento(-a) * f, ze, "Tab. C3.3.IX");
                        }
                        break;

                    case SchemaFaldeMultiple.FaldeDoppieConImpluvi:
                        FaldaSingola(r, tratto + ", falda discendente", -a, ze, fattore);
                        Cpe(r, tratto + ", falda ascendente", Unica, CoefficientiPressione.FaldaDoppiaSottovento(-a) * fattore, ze, "Tab. C3.3.IX");
                        break;
                }
            }
        }

        /// <summary>
        /// Coperture a volta cilindrica (§ C3.3.8.1.7 e Figura C3.3.19)
        /// </summary>
        private static void VoltaCilindrica(GeometriaCostruzione g, RisultatoCoefficienti r, bool perpendicolare)
        {
            double ze = g.H + g.Freccia / 2;
            double fSuD = g.Freccia / g.D;
            double hSuD = g.H / g.D;

            if (!perpendicolare)
            {
                r.Avvertenze.Add("Vento parallelo alle generatrici: in prima approssimazione si applicano i coefficienti delle coperture piane.");
                CoperturaPiana(g, r, ze);
                return;
            }

            if (fSuD <= 0.05)
            {
                r.Avvertenze.Add("f/d ≤ 0,05: si adottano i coefficienti delle coperture piane.");
                CoperturaPiana(g, r, ze);
                return;
            }

            if (fSuD > 0.5)
            {
                r.Avvertenze.Add("f/d > 0,5: oltre il campo della Fig. C3.3.19; adottati i valori limite.");
            }

            const string rif = "Fig. C3.3.19 (c)";
            double[] zonaA = CoefficientiPressione.VoltaZonaA(hSuD, fSuD);
            if (zonaA.Length == 1)
            {
                Cpe(r, "Volta, zona A (sopravento)", Unica, zonaA[0], ze, rif);
            }
            else
            {
                Cpe(r, "Volta, zona A (sopravento)", Negativi, zonaA[0], ze, rif);
                Cpe(r, "Volta, zona A (sopravento)", Positivi, zonaA[1], ze, rif);
            }

            Cpe(r, "Volta, zone B (intermedie)", Unica, CoefficientiPressione.VoltaZonaB(fSuD), ze, rif);
            Cpe(r, "Volta, zona C (sottovento)", Unica, CoefficientiPressione.VoltaZonaC(fSuD), ze, rif);
        }

        #endregion

        #region tettoie

        private static void CalcolaTettoia(GeometriaCostruzione g, RisultatoCoefficienti r)
        {
            double ze = g.H;
            double fi = Math.Clamp(g.GradoBloccaggio, 0, 1);
            bool perpendicolare = g.Direzione == DirezioneVento.PerpendicolareAlColmo;
            double coseno = Math.Cos(Math.Abs(g.Alfa) * Math.PI / 180);
            if (coseno < 0.1) coseno = 0.1;

            if (g.GradoBloccaggio < 0 || g.GradoBloccaggio > 1)
            {
                r.Avvertenze.Add("Il grado di bloccaggio φ deve essere compreso fra 0 e 1.");
            }

            int tratti = g.Tipologia == TipologiaCostruzione.TettoiaFaldeMultiple ? Math.Max(g.NumeroTratti, 1) : 1;
            int faldePerTratto = g.Tipologia == TipologiaCostruzione.TettoiaFaldaSingola ? 1 : 2;

            // con vento parallelo al colmo le falde si sviluppano lungo b
            double larghezzaFalda = (perpendicolare ? g.D : g.B) / (tratti * faldePerTratto) / coseno;
            double lunghezzaFalda = perpendicolare ? g.B : g.D;
            double area = larghezzaFalda * lunghezzaFalda;

            if (!perpendicolare)
            {
                r.Avvertenze.Add("Vento parallelo al colmo: ciascuna falda è analizzata come tettoia piana a falda singola (α = 0°).");
                Forza(r, "Ciascuna falda", "cF > 0 (verso il basso)", CoefficientiPressione.TettoiaSingolaPositivo(0), ze, area, "Tab. C3.3.XV");
                Forza(r, "Ciascuna falda", "cF < 0 (verso l'alto)", CoefficientiPressione.TettoiaSingolaNegativo(0, fi), ze, area, "Tab. C3.3.XV");
                return;
            }

            switch (g.Tipologia)
            {
                case TipologiaCostruzione.TettoiaFaldaSingola:
                    if (Math.Abs(g.Alfa) > 30)
                        r.Avvertenze.Add("Inclinazione oltre 30°: fuori dal campo della Tab. C3.3.XV.");
                    Forza(r, "Falda", "cF > 0 (verso il basso)", CoefficientiPressione.TettoiaSingolaPositivo(g.Alfa), ze, area, "Tab. C3.3.XV");
                    Forza(r, "Falda", "cF < 0 (verso l'alto)", CoefficientiPressione.TettoiaSingolaNegativo(g.Alfa, fi), ze, area, "Tab. C3.3.XV");
                    break;

                case TipologiaCostruzione.TettoiaFaldaDoppia:
                    AvvisoCampoTettoiaDoppia(g, r);
                    Forza(r, "Ciascuna falda", "cF > 0 (verso il basso)", CoefficientiPressione.TettoiaDoppiaPositivo(g.Alfa), ze, area, "Tab. C3.3.XVI");
                    Forza(r, "Ciascuna falda", "cF < 0 (verso l'alto)", CoefficientiPressione.TettoiaDoppiaNegativo(g.Alfa, fi), ze, area, "Tab. C3.3.XVI");
                    break;

                case TipologiaCostruzione.TettoiaFaldeMultiple:
                    AvvisoCampoTettoiaDoppia(g, r);
                    double positivo = CoefficientiPressione.TettoiaDoppiaPositivo(g.Alfa);
                    double negativo = CoefficientiPressione.TettoiaDoppiaNegativo(g.Alfa, fi);
                    int gruppi = Math.Min((tratti + 1) / 2, 3);
                    for (int i = 1; i <= gruppi; i++)
                    {
                        string campo = i == 1 ? "Primo campo" : (i == 2 ? "Secondo campo" : "Altri campi");
                        Forza(r, campo + ", ciascuna falda", "cF > 0 (verso il basso)",
                            positivo * CoefficientiPressione.TettoiaMultiplaFattoreRiduttivo(i, true), ze, area, "Tab. C3.3.XVI - XVII");
                        Forza(r, campo + ", ciascuna falda", "cF < 0 (verso l'alto)",
                            negativo * CoefficientiPressione.TettoiaMultiplaFattoreRiduttivo(i, false), ze, area, "Tab. C3.3.XVI - XVII");
                    }
                    break;
            }
        }

        private static void AvvisoCampoTettoiaDoppia(GeometriaCostruzione g, RisultatoCoefficienti r)
        {
            if (g.Alfa < -20 || g.Alfa > 30)
            {
                r.Avvertenze.Add("Inclinazione fuori dal campo -20° ÷ +30° della Tab. C3.3.XVI.");
            }
        }

        #endregion

        #region utilità

        private static string FasciaSopravento(GeometriaCostruzione g)
        {
            double profondità = Math.Min(Math.Min(g.B / 2, g.AltezzaSommità), g.D);
            return "fascia sopravento (" + profondità.ToString("0.##", CultureInfo.CurrentCulture) + " m)";
        }

        private static void Cpe(RisultatoCoefficienti r, string superficie, string condizione, double valore, double ze, string riferimento)
        {
            r.Coefficienti.Add(new CoefficienteSuperficie(superficie, condizione, valore, ze, TipoCoefficiente.PressioneEsterna, 0, riferimento));
        }

        private static void Forza(RisultatoCoefficienti r, string superficie, string condizione, double valore, double ze, double area, string riferimento)
        {
            r.Coefficienti.Add(new CoefficienteSuperficie(superficie, condizione, valore, ze, TipoCoefficiente.Forza, area, riferimento));
        }

        #endregion
    }
}
