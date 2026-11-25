using System;
using System.Globalization;

namespace Vento.Aerodinamica
{
    /// <summary>
    /// Compone, per la tipologia di costruzione scelta, l'elenco dei coefficienti aerodinamici
    /// globali da applicare alle singole superfici (Circolare 21/01/2019 n. 7, § C3.3.8.1 e § C3.3.8.2).
    /// Per ogni zona della copertura indica anche i tratti occupati lungo la direzione del vento,
    /// in ascissa orizzontale x e in ascissa s misurata sullo sviluppo, entrambe dal bordo sopravento.
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
            double hSuD = AltezzaPareti(g) / g.D;
            switch (faccia)
            {
                case FacciaEdificio.Sopravento: return CoefficientiPressione.PareteSopravento(hSuD);
                case FacciaEdificio.Laterale: return CoefficientiPressione.PareteLaterale(hSuD);
                default: return CoefficientiPressione.PareteSottovento(hSuD);
            }
        }

        /// <summary>
        /// Sviluppo della volta cilindrica: raggio R = (d²/4 + f²)/(2f), semiangolo al centro θ = asin(d/2R)
        /// (oltre il semicerchio, per f > d/2, θ = π − asin(d/2R)), lunghezza dell'arco L = 2·R·θ.
        /// </summary>
        /// <param name="d">corda della volta [m]</param>
        /// <param name="f">freccia [m]</param>
        /// <param name="raggio">raggio dell'arco [m]</param>
        /// <param name="semiAngolo">semiangolo al centro [rad]</param>
        /// <returns>lunghezza dell'arco [m]</returns>
        public static double SviluppoVolta(double d, double f, out double raggio, out double semiAngolo)
        {
            raggio = (d * d / 4 + f * f) / (2 * f);
            double seno = Math.Min(d / 2 / raggio, 1);
            semiAngolo = f <= d / 2 ? Math.Asin(seno) : Math.PI - Math.Asin(seno);
            return 2 * raggio * semiAngolo;
        }

        #region pareti

        /// <summary>
        /// Altezza h da usare nel rapporto h/d delle pareti (Tab. C3.3.I): la quota massima della copertura;
        /// per la volta cilindrica la quota di imposta, cioè la h della Fig. C3.3.19, la stessa del rapporto h/d della volta.
        /// </summary>
        private static double AltezzaPareti(GeometriaCostruzione g)
        {
            return g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica ? g.H : g.AltezzaSommità;
        }

        /// <summary>
        /// Quota di riferimento ze delle pareti [m]: la quota di sommità; per la volta cilindrica ze = h + f/2,
        /// la stessa quota di riferimento della copertura (§ C3.3.8.1.7).
        /// </summary>
        private static double QuotaPareti(GeometriaCostruzione g)
        {
            return g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica ? g.H + g.Freccia / 2 : g.AltezzaSommità;
        }

        private static void AggiungiPareti(GeometriaCostruzione g, RisultatoCoefficienti r)
        {
            const string rif = "Tab. C3.3.I";
            bool volta = g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica;
            double h = QuotaPareti(g);
            double hSuD = AltezzaPareti(g) / g.D;
            double sopravento = CoefficientiPressione.PareteSopravento(hSuD);
            Val(r, "h/d per le pareti", "h / d = " + F(AltezzaPareti(g)) + " / " + F(g.D), F(hSuD, "0.000"),
                (volta ? "h è la quota di imposta (Fig. C3.3.19). " : "") + (hSuD <= 0.5 ? "h/d ≤ 0,5" : hSuD <= 1 ? "0,5 < h/d ≤ 1" : hSuD <= 5 ? "1 < h/d ≤ 5" : "h/d > 5, fuori campo") + " · Tab. C3.3.I");
            string quota = volta ? "ze = h + f/2 = " + F(g.H) + " + " + F(g.Freccia) + "/2" : "ze = h";
            Val(r, "Quota di riferimento delle pareti", h <= g.B ? quota : "ze > b: ze = b fino a z = b, poi " + quota, h <= g.B ? F(h) + " m" : F(g.B) + " m e " + F(h) + " m",
                (volta ? "§ C3.3.8.1.7: stessa quota di riferimento della volta. " : "§ C3.3.8.1.1.1. ") + "Facce laterali e sottovento: stessa ze");

            if (h <= g.B)
            {
                Cpe(r, "Parete sopravento", Unica, sopravento, h, rif, ParteCostruzione.PareteSopravento);
            }
            else
            {
                // edificio alto: due zone con quote di riferimento distinte (§ C3.3.8.1.1.1, criterio 1)
                Cpe(r, "Parete sopravento, z ≤ b", Unica, sopravento, g.B, rif, ParteCostruzione.PareteSopravento);
                Cpe(r, "Parete sopravento, b < z ≤ h", Unica, sopravento, h, rif, ParteCostruzione.PareteSopravento);
            }

            Cpe(r, "Pareti laterali", Unica, CoefficientiPressione.PareteLaterale(hSuD), h, rif, ParteCostruzione.PareteLaterale);
            Cpe(r, "Parete sottovento", Unica, CoefficientiPressione.PareteSottovento(hSuD), h, rif, ParteCostruzione.PareteSottovento);

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

            if (aFalde)
            {
                double modulo = Math.Abs(g.Alfa);
                Val(r, "Inclinazione delle falde α", "dato", F(g.Alfa) + "°",
                    modulo <= 5 ? "|α| ≤ 5°: copertura piana (§ C3.3.8.1.2)" : modulo <= 45 && g.Alfa > 0 ? "5° < α ≤ 45°: valori di entrambi i segni (Tab. C3.3.V)" : modulo > 75 ? "oltre 75°, fuori campo" : "entro il campo delle tabelle");
            }
            if (!aFalde || Math.Abs(g.Alfa) <= 5 || !perpendicolare)
            {
                double e = ProfonditàFascia(g);
                if (g.Tipologia != TipologiaCostruzione.CoperturaVoltaCilindrica && g.Tipologia != TipologiaCostruzione.CoperturaPadiglione || Math.Abs(g.Alfa) <= 5 && aFalde)
                    Val(r, "Profondità della fascia sopravento", "min(b/2; h) = min(" + F(g.B / 2) + "; " + F(g.AltezzaSommità) + ")", F(e) + " m", "zona A; le restanti zone sono la zona B");
            }
            Val(r, "Quota di riferimento della copertura", g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica ? "ze = h + f/2 = " + F(g.H) + " + " + F(g.Freccia) + "/2" : "ze = quota massima della copertura",
                F(g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica ? g.H + g.Freccia / 2 : ze) + " m", g.Tipologia == TipologiaCostruzione.CoperturaVoltaCilindrica ? "§ C3.3.8.1.7" : "§ C3.3.8.1");

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

            double coseno = Coseno(g.Alfa);
            switch (g.Tipologia)
            {
                case TipologiaCostruzione.CoperturaPiana:
                    CoperturaPiana(g, r, ze);
                    break;

                case TipologiaCostruzione.CoperturaFaldaSingola:
                    if (perpendicolare) FaldaSingola(r, "Falda", g.Alfa, ze, 1, Falda(0, g.D, coseno));
                    else FaldaSingolaParallela(g, r, g.Alfa, ze);
                    break;

                case TipologiaCostruzione.CoperturaFaldaDoppia:
                    if (perpendicolare)
                    {
                        FaldaSingola(r, "Falda sopravento", g.Alfa, ze, 1, Falda(0, g.D / 2, coseno));
                        Cpe(r, "Falda sottovento", Unica, CoefficientiPressione.FaldaDoppiaSottovento(g.Alfa), ze, "Tab. C3.3.IX",
                            ParteCostruzione.Copertura, Falda(g.D / 2, g.D, coseno));
                    }
                    else
                    {
                        FaldaDoppiaParallela(g, r, g.Alfa, ze);
                    }
                    break;

                case TipologiaCostruzione.CoperturaPadiglione:
                    // i tratti servono solo alla figura: la profondità reale delle falde dipende dalla pianta
                    double a = Math.Abs(g.Alfa);
                    FaldaSingola(r, "Falda sopravento", a, ze, 1, Falda(0, g.D / 2, coseno), esatta: false);
                    Cpe(r, "Falda sottovento", Unica, CoefficientiPressione.FaldaDoppiaSottovento(a), ze, "Tab. C3.3.IX",
                        ParteCostruzione.Copertura, Falda(g.D / 2, g.D, coseno), esatta: false);
                    Cpe(r, "Falde laterali", Unica, CoefficientiPressione.PadiglioneFaldeLaterali(a), ze, "Tab. C3.3.XIII", ParteCostruzione.FaldaLaterale);
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
            double e = ProfonditàFascia(g);
            Cpe(r, "Copertura, " + FasciaSopravento(g), Unica, CoefficientiPressione.CoperturaPianaFasciaSopravento, ze, rif, ParteCostruzione.Copertura, Piano(0, e));
            if (e < g.D)
            {
                Cpe(r, "Copertura, restanti zone", Negativi, -CoefficientiPressione.CoperturaPianaRestantiZone, ze, rif, ParteCostruzione.Copertura, Piano(e, g.D));
                Cpe(r, "Copertura, restanti zone", Positivi, CoefficientiPressione.CoperturaPianaRestantiZone, ze, rif, ParteCostruzione.Copertura, Piano(e, g.D));
            }
        }

        /// <summary>
        /// Falda trattata come falda singola con vento perpendicolare al colmo (Tab. C3.3.V),
        /// con eventuale fattore riduttivo delle falde multiple
        /// </summary>
        private static void FaldaSingola(RisultatoCoefficienti r, string nome, double alfa, double ze, double fattore,
            TrattoSviluppo[] tratti, bool esatta = true)
        {
            const string rif = "Tab. C3.3.V";
            double? negativo = CoefficientiPressione.FaldaSingolaNegativo(alfa);
            double? positivo = CoefficientiPressione.FaldaSingolaPositivo(alfa);

            if (negativo.HasValue) Cpe(r, nome, positivo.HasValue ? Negativi : Unica, negativo.Value * fattore, ze, rif, ParteCostruzione.Copertura, tratti, esatta);
            if (positivo.HasValue) Cpe(r, nome, negativo.HasValue ? Positivi : Unica, positivo.Value * fattore, ze, rif, ParteCostruzione.Copertura, tratti, esatta);
        }

        private static void FaldaSingolaParallela(GeometriaCostruzione g, RisultatoCoefficienti r, double alfa, double ze)
        {
            const string rif = "Tab. C3.3.VI";
            double e = ProfonditàFascia(g);
            Cpe(r, "Copertura, " + FasciaSopravento(g), Unica, CoefficientiPressione.FaldaSingolaParallelaFasciaSopravento(alfa), ze, rif, ParteCostruzione.Copertura, Piano(0, e));
            if (e < g.D)
                Cpe(r, "Copertura, restanti zone", Unica, CoefficientiPressione.FaldaSingolaParallelaRestantiZone(alfa), ze, rif, ParteCostruzione.Copertura, Piano(e, g.D));
        }

        private static void FaldaDoppiaParallela(GeometriaCostruzione g, RisultatoCoefficienti r, double alfa, double ze)
        {
            const string rif = "Tab. C3.3.X";
            double e = ProfonditàFascia(g);
            Cpe(r, "Copertura, " + FasciaSopravento(g), Unica, CoefficientiPressione.FaldaDoppiaParallelaFasciaSopravento(alfa), ze, rif, ParteCostruzione.Copertura, Piano(0, e));
            if (e < g.D)
                Cpe(r, "Copertura, restanti zone", Unica, CoefficientiPressione.FaldaDoppiaParallelaRestantiZone(alfa), ze, rif, ParteCostruzione.Copertura, Piano(e, g.D));
        }

        /// <summary>
        /// Coperture a falde multiple (§ C3.3.8.1.6 e Figura C3.3.18).
        /// Il passo è d/n. Negli shed (a, b) e nelle falde con impluvi (d) ogni tratto coincide con un passo.
        /// Nelle falde con colmi (c) il primo tratto è la sola prima falda; i successivi sono gli impluvi
        /// (falda discendente + falda ascendente) e l'ultimo comprende la falda discendente finale: n + 1 tratti.
        /// </summary>
        private static void FaldeMultiple(GeometriaCostruzione g, RisultatoCoefficienti r, double ze, bool perpendicolare)
        {
            double a = Math.Abs(g.Alfa);
            int n = Math.Max(g.NumeroTratti, 1);

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

            double coseno = Coseno(a);
            double passo = g.D / n;
            Val(r, "Passo dei tratti", "d / n = " + F(g.D) + " / " + n, F(passo) + " m", "fattori riduttivi 1 · 0,8 · 0,6 dal primo tratto sopravento (Fig. C3.3.18)");
            double mezzo = passo / 2;
            bool colmi = g.SchemaMultiplo == SchemaFaldeMultiple.FaldeDoppieConColmi;
            int tratti = colmi ? n + 1 : n;

            // gruppi di tratti con lo stesso fattore riduttivo: 1°, 2°, dal 3° in poi
            int gruppi = Math.Min(tratti, 3);
            for (int i = 1; i <= gruppi; i++)
            {
                int ultimo = i < 3 ? i : tratti;
                string tratto = i < 3 || tratti == 3 ? "Tratto " + i : "Tratti 3-" + tratti;
                double fattore = i == 1 ? 1.0 : (i == 2 ? 0.8 : 0.6);

                switch (g.SchemaMultiplo)
                {
                    case SchemaFaldeMultiple.ShedInclinazioneNegativa:
                        FaldaSingola(r, tratto + ", falda", -a, ze, fattore, Falda((i - 1) * passo, ultimo * passo, coseno));
                        break;

                    case SchemaFaldeMultiple.ShedInclinazionePositiva:
                        {
                            const string rif = "Tab. C3.3.V - Fig. C3.3.18 (b)";
                            TrattoSviluppo[] zona = Falda((i - 1) * passo, ultimo * passo, coseno);
                            double? negativo = CoefficientiPressione.FaldaSingolaNegativo(a);
                            double? positivo = CoefficientiPressione.FaldaSingolaPositivo(a);
                            if (negativo.HasValue)
                                Cpe(r, tratto + ", falda", Negativi, negativo.Value * fattore, ze, rif, ParteCostruzione.Copertura, zona);
                            if (positivo.HasValue)
                                Cpe(r, tratto + ", falda", Positivi, i == 1 ? positivo.Value : -0.4, ze, rif, ParteCostruzione.Copertura, zona);
                        }
                        break;

                    case SchemaFaldeMultiple.FaldeDoppieConColmi:
                        if (i == 1)
                        {
                            FaldaSingola(r, "Tratto 1, prima falda sopravento", a, ze, 1, Falda(0, mezzo, coseno));
                        }
                        else
                        {
                            // tratti successivi: falda doppia con inclinazione negativa, ridotta di 0,6 dal terzo tratto.
                            // Il tratto k (k ≥ 2) occupa le mezze falde 2k−2 (discendente) e 2k−1 (ascendente, assente nell'ultimo).
                            double f = i == 2 ? 1.0 : 0.6;
                            var discendenti = new System.Collections.Generic.List<TrattoSviluppo>();
                            var ascendenti = new System.Collections.Generic.List<TrattoSviluppo>();
                            for (int k = i; k <= ultimo; k++)
                            {
                                discendenti.AddRange(Falda((2 * k - 3) * mezzo, (2 * k - 2) * mezzo, coseno));
                                if (2 * k - 1 <= 2 * n) ascendenti.AddRange(Falda((2 * k - 2) * mezzo, (2 * k - 1) * mezzo, coseno));
                            }
                            FaldaSingola(r, tratto + ", falda discendente", -a, ze, f, discendenti.ToArray());
                            if (ascendenti.Count > 0)
                                Cpe(r, tratto + ", falda ascendente", Unica, CoefficientiPressione.FaldaDoppiaSottovento(-a) * f, ze, "Tab. C3.3.IX",
                                    ParteCostruzione.Copertura, ascendenti.ToArray());
                        }
                        break;

                    case SchemaFaldeMultiple.FaldeDoppieConImpluvi:
                        {
                            var discendenti = new System.Collections.Generic.List<TrattoSviluppo>();
                            var ascendenti = new System.Collections.Generic.List<TrattoSviluppo>();
                            for (int k = i; k <= ultimo; k++)
                            {
                                discendenti.AddRange(Falda((k - 1) * passo, (k - 1) * passo + mezzo, coseno));
                                ascendenti.AddRange(Falda((k - 1) * passo + mezzo, k * passo, coseno));
                            }
                            FaldaSingola(r, tratto + ", falda discendente", -a, ze, fattore, discendenti.ToArray());
                            Cpe(r, tratto + ", falda ascendente", Unica, CoefficientiPressione.FaldaDoppiaSottovento(-a) * fattore, ze, "Tab. C3.3.IX",
                                ParteCostruzione.Copertura, ascendenti.ToArray());
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Coperture a volta cilindrica (§ C3.3.8.1.7 e Figura C3.3.19): con vento perpendicolare alle generatrici
        /// la volta è divisa in quattro zone di uguale sviluppo, A (sopravento), B, B, C (sottovento).
        /// </summary>
        private static void VoltaCilindrica(GeometriaCostruzione g, RisultatoCoefficienti r, bool perpendicolare)
        {
            double ze = g.H + g.Freccia / 2;
            double fSuD = g.Freccia / g.D;
            double hSuD = g.H / g.D;
            Val(r, "h, quota di imposta", "dato", F(g.H) + " m", "altezza delle pareti sotto la volta");
            Val(r, "d, corda della volta", "dato", F(g.D) + " m", "dimensione parallela al vento");
            Val(r, "f, freccia", "dato", F(g.Freccia) + " m", "");
            Val(r, "h/d", F(g.H) + " / " + F(g.D), F(hSuD, "0.000"),
                hSuD >= 0.5 ? "h/d ≥ 0,5: curve h/d ≥ 0,5 della Fig. C3.3.19 (c)" : hSuD <= 0 ? "h/d = 0: curva h/d = 0" : "0 < h/d < 0,5: interpolazione lineare fra le curve h/d = 0 e h/d ≥ 0,5");
            Val(r, "f/d", F(g.Freccia) + " / " + F(g.D), F(fSuD, "0.000"),
                fSuD <= 0.05 ? "f/d ≤ 0,05: coefficienti delle coperture piane" : fSuD < 0.2 ? "0,05 < f/d < 0,2: zona A con un solo valore" :
                fSuD <= 0.3 ? "0,2 ≤ f/d ≤ 0,3: zona A con due valori, da considerare entrambi" : fSuD <= 0.5 ? "0,3 < f/d ≤ 0,5: zona A con un solo valore" : "f/d > 0,5: oltre il campo del grafico");

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
            double sviluppo = SviluppoVolta(g.D, g.Freccia, out double raggio, out _);
            Val(r, "Raggio della volta R", "(d²/4 + f²) / (2f)", F(raggio, "0.00") + " m", "");
            Val(r, "Sviluppo della volta L", "2 · R · θ", F(sviluppo, "0.00") + " m", "quattro zone di uguale sviluppo: A, B, B, C");
            Val(r, "Sviluppo di ciascuna zona", "L / 4", F(sviluppo / 4, "0.00") + " m", "§ C3.3.8.1.7");
            CoefficientiPressione.VoltaZonaACurve(fSuD, out double altezzaNulla, out double? ramoNegativo, out double? ramoPositivo);
            double peso = CoefficientiPressione.VoltaPesoAltezza(hSuD);
            Val(r, "cpe,A per h/d = 0", "lettura a f/d = " + F(fSuD, "0.000"), S(altezzaNulla), rif);
            if (ramoNegativo.HasValue) Val(r, "cpe,A per h/d ≥ 0,5, ramo negativo", "lettura a f/d = " + F(fSuD, "0.000"), S(ramoNegativo.Value), rif);
            if (ramoPositivo.HasValue) Val(r, "cpe,A per h/d ≥ 0,5, ramo positivo", "lettura a f/d = " + F(fSuD, "0.000"), S(ramoPositivo.Value), rif);
            Val(r, "Peso dell'interpolazione su h/d", "min(h/d; 0,5) / 0,5", F(peso, "0.000"), "cpe,A = cpe,A(0) + peso · [cpe,A(≥0,5) − cpe,A(0)]");
            TrattoSviluppo[] zonaASviluppo = Arco(g, 0, 0.25);
            double[] zonaA = CoefficientiPressione.VoltaZonaA(hSuD, fSuD);
            Val(r, "cpe,A adottato", "interpolazione su h/d", string.Join(" e ", System.Linq.Enumerable.Select(zonaA, S)), zonaA.Length > 1 ? "due condizioni di carico" : "");
            Val(r, "cpe,B", "lettura a f/d = " + F(fSuD, "0.000"), S(CoefficientiPressione.VoltaZonaB(fSuD)), rif + ", indipendente da h/d");
            Val(r, "cpe,C", "lettura a f/d = " + F(fSuD, "0.000"), S(CoefficientiPressione.VoltaZonaC(fSuD)), rif + ", indipendente da h/d");
            if (zonaA.Length == 1)
            {
                Cpe(r, "Volta, zona A (sopravento)", Unica, zonaA[0], ze, rif, ParteCostruzione.Copertura, zonaASviluppo);
            }
            else
            {
                Cpe(r, "Volta, zona A (sopravento)", Negativi, zonaA[0], ze, rif, ParteCostruzione.Copertura, zonaASviluppo);
                Cpe(r, "Volta, zona A (sopravento)", Positivi, zonaA[1], ze, rif, ParteCostruzione.Copertura, zonaASviluppo);
            }

            Cpe(r, "Volta, zone B (intermedie)", Unica, CoefficientiPressione.VoltaZonaB(fSuD), ze, rif, ParteCostruzione.Copertura, Arco(g, 0.25, 0.75));
            Cpe(r, "Volta, zona C (sottovento)", Unica, CoefficientiPressione.VoltaZonaC(fSuD), ze, rif, ParteCostruzione.Copertura, Arco(g, 0.75, 1));
        }

        #endregion

        #region tettoie

        private static void CalcolaTettoia(GeometriaCostruzione g, RisultatoCoefficienti r)
        {
            double ze = g.H;
            double fi = Math.Clamp(g.GradoBloccaggio, 0, 1);
            bool perpendicolare = g.Direzione == DirezioneVento.PerpendicolareAlColmo;
            double coseno = Coseno(g.Alfa);

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
            Val(r, "Quota di riferimento", "ze = h", F(ze) + " m", "altezza massima della tettoia (§ C3.3.8.2)");
            Val(r, "Grado di bloccaggio φ", "dato", F(fi), fi <= 0 ? "tettoia libera" : fi >= 1 ? "tettoia ostruita" : "interpolazione lineare fra φ = 0 e φ = 1");
            Val(r, "Area di riferimento della falda L²", F(larghezzaFalda, "0.00") + " · " + F(lunghezzaFalda, "0.00"), F(area, "0.00") + " m²", "F = p · L², applicata a d/4 dal bordo sopravento della falda");

            if (!perpendicolare)
            {
                r.Avvertenze.Add("Vento parallelo al colmo: ciascuna falda è analizzata come tettoia piana a falda singola (α = 0°).");
                Forza(r, "Ciascuna falda", "cF > 0 (verso il basso)", CoefficientiPressione.TettoiaSingolaPositivo(0), ze, area, "Tab. C3.3.XV", Piano(0, g.D));
                Forza(r, "Ciascuna falda", "cF < 0 (verso l'alto)", CoefficientiPressione.TettoiaSingolaNegativo(0, fi), ze, area, "Tab. C3.3.XV", Piano(0, g.D));
                return;
            }

            switch (g.Tipologia)
            {
                case TipologiaCostruzione.TettoiaFaldaSingola:
                    if (Math.Abs(g.Alfa) > 30)
                        r.Avvertenze.Add("Inclinazione oltre 30°: fuori dal campo della Tab. C3.3.XV.");
                    Forza(r, "Falda", "cF > 0 (verso il basso)", CoefficientiPressione.TettoiaSingolaPositivo(g.Alfa), ze, area, "Tab. C3.3.XV", Falda(0, g.D, coseno));
                    Forza(r, "Falda", "cF < 0 (verso l'alto)", CoefficientiPressione.TettoiaSingolaNegativo(g.Alfa, fi), ze, area, "Tab. C3.3.XV", Falda(0, g.D, coseno));
                    break;

                case TipologiaCostruzione.TettoiaFaldaDoppia:
                    {
                        AvvisoCampoTettoiaDoppia(g, r);
                        var falde = new[] { Falda(0, g.D / 2, coseno)[0], Falda(g.D / 2, g.D, coseno)[0] };
                        Forza(r, "Ciascuna falda", "cF > 0 (verso il basso)", CoefficientiPressione.TettoiaDoppiaPositivo(g.Alfa), ze, area, "Tab. C3.3.XVI", falde);
                        Forza(r, "Ciascuna falda", "cF < 0 (verso l'alto)", CoefficientiPressione.TettoiaDoppiaNegativo(g.Alfa, fi), ze, area, "Tab. C3.3.XVI", falde);
                    }
                    break;

                case TipologiaCostruzione.TettoiaFaldeMultiple:
                    {
                        AvvisoCampoTettoiaDoppia(g, r);
                        double positivo = CoefficientiPressione.TettoiaDoppiaPositivo(g.Alfa);
                        double negativo = CoefficientiPressione.TettoiaDoppiaNegativo(g.Alfa, fi);
                        double passo = g.D / tratti;
                        int gruppi = Math.Min((tratti + 1) / 2, 3);
                        for (int i = 1; i <= gruppi; i++)
                        {
                            // i campi si contano dai due bordi: il gruppo i comprende i campi a distanza i dal bordo più vicino (dal terzo in poi tutti gli interni)
                            var campi = new System.Collections.Generic.List<TrattoSviluppo>();
                            for (int k = 1; k <= tratti; k++)
                            {
                                int dalBordo = Math.Min(k, tratti + 1 - k);
                                if (dalBordo == i || (i == 3 && dalBordo >= 3)) campi.AddRange(Falda((k - 1) * passo, k * passo, coseno));
                            }
                            string campo = i == 1 ? "Primo campo" : (i == 2 ? "Secondo campo" : "Altri campi");
                            Forza(r, campo + ", ciascuna falda", "cF > 0 (verso il basso)",
                                positivo * CoefficientiPressione.TettoiaMultiplaFattoreRiduttivo(i, true), ze, area, "Tab. C3.3.XVI - XVII", campi.ToArray());
                            Forza(r, campo + ", ciascuna falda", "cF < 0 (verso l'alto)",
                                negativo * CoefficientiPressione.TettoiaMultiplaFattoreRiduttivo(i, false), ze, area, "Tab. C3.3.XVI - XVII", campi.ToArray());
                        }
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

        #region sviluppo delle zone

        /// <summary>Coseno dell'inclinazione, limitato per evitare sviluppi infiniti con falde quasi verticali</summary>
        private static double Coseno(double alfa)
        {
            return Math.Max(Math.Cos(Math.Abs(alfa) * Math.PI / 180), 0.1);
        }

        /// <summary>Tratto orizzontale: lo sviluppo coincide con l'ascissa</summary>
        private static TrattoSviluppo[] Piano(double x0, double x1)
        {
            return new[] { new TrattoSviluppo(x0, x1, x0, x1) };
        }

        /// <summary>Tratto di falde con la stessa inclinazione: s = x / cos α</summary>
        private static TrattoSviluppo[] Falda(double x0, double x1, double coseno)
        {
            return new[] { new TrattoSviluppo(x0, x1, x0 / coseno, x1 / coseno) };
        }

        /// <summary>
        /// Tratto di volta fra due frazioni dello sviluppo (0 = imposta sopravento, 1 = imposta sottovento).
        /// L'ascissa orizzontale è x = d/2 + R·sin φ, con φ che varia da −θ a +θ lungo l'arco.
        /// </summary>
        private static TrattoSviluppo[] Arco(GeometriaCostruzione g, double da, double a)
        {
            double sviluppo = SviluppoVolta(g.D, g.Freccia, out double raggio, out double semiAngolo);
            double X(double frazione) => g.D / 2 + raggio * Math.Sin(-semiAngolo + 2 * semiAngolo * frazione);
            return new[] { new TrattoSviluppo(X(da), X(a), sviluppo * da, sviluppo * a) };
        }

        /// <summary>Profondità della fascia sopravento: min(b/2; h), non oltre d [m]</summary>
        private static double ProfonditàFascia(GeometriaCostruzione g)
        {
            return Math.Min(Math.Min(g.B / 2, g.AltezzaSommità), g.D);
        }

        #endregion

        #region utilità

        /// <summary>Numero nella cultura corrente, senza zeri superflui</summary>
        private static string F(double valore, string formato = "0.###")
        {
            return valore.ToString(formato, CultureInfo.GetCultureInfo("it-IT"));
        }

        /// <summary>Coefficiente con segno esplicito</summary>
        private static string S(double valore)
        {
            return valore.ToString("+0.00;-0.00;0.00", CultureInfo.GetCultureInfo("it-IT"));
        }

        private static void Val(RisultatoCoefficienti r, string grandezza, string espressione, string valore, string nota)
        {
            r.Valutazioni.Add(new Valutazione(grandezza, espressione, valore, nota));
        }

        private static string FasciaSopravento(GeometriaCostruzione g)
        {
            return "fascia sopravento (" + ProfonditàFascia(g).ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) + " m)";
        }

        private static void Cpe(RisultatoCoefficienti r, string superficie, string condizione, double valore, double ze, string riferimento,
            ParteCostruzione parte, TrattoSviluppo[] tratti = null, bool esatta = true)
        {
            r.Coefficienti.Add(new CoefficienteSuperficie(superficie, condizione, valore, ze, TipoCoefficiente.PressioneEsterna, 0, riferimento)
            {
                Parte = parte, Tratti = tratti ?? Array.Empty<TrattoSviluppo>(), EstensioneEsatta = esatta
            });
        }

        private static void Forza(RisultatoCoefficienti r, string superficie, string condizione, double valore, double ze, double area, string riferimento,
            TrattoSviluppo[] tratti)
        {
            r.Coefficienti.Add(new CoefficienteSuperficie(superficie, condizione, valore, ze, TipoCoefficiente.Forza, area, riferimento)
            {
                Parte = ParteCostruzione.Copertura, Tratti = tratti
            });
        }

        #endregion
    }
}
