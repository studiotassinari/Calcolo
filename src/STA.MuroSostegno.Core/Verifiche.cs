// Motore importato da TA-MuroDiSostegno: formule conservate per confronto con il programma originale.
// Limiti noti e validazione documentati in docs/MURO-SOSTEGNO.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static STA.MuroSostegno.Core.Legacy.SpinteDelTerreno;

namespace STA.MuroSostegno.Core.Legacy.Geotecnica
{
    internal class Verifiche
    {
        public Dictionary<string, double> A1_fav    = new Dictionary<string, double>();
        Dictionary<string, double>        A1_sfav   = new Dictionary<string, double>();
        Dictionary<string, double>        M1        = new Dictionary<string, double>();
        Dictionary<string, double>        R3        = new Dictionary<string, double>();
        Dictionary<string, double>        VerSismica = new Dictionary<string, double>();

        public GeomAnalitica geom = new GeomAnalitica();

        // ── Coefficienti vettoriali NTC2018 ─────────────────────────────────
        // Ordine: [G1, G2, Qk]
        public double[] Coeff_A1_Fav = [ 1.0, 0.8, 0.0 ]; // A1 fav
        public double[] Coeff_A1_SFav = [ 1.3, 1.5, 1.5 ]; // A1 sfav  ← era 1.1
        public double[] Coeff_M1 = [ 1.0, 1.0, 1.0, 1.0 ];

        // ── Costruttore di default ───────────────────────────────────────────
        public Verifiche()
        {
            InizializzaCoefficienti();
        }

        // ── Costruttore con parametri geometrici e geotecnici ────────────────
        public Verifiche(Punto[] Muro, Punto[] T_ant, Punto[] T_post,
            double Gamma_M, double Gamma_T,
            double fi,          // φ' già in radianti
            double delta,       // δ  già in radianti
            double c1, double sovraccarico,
            double ro, double H, double Vs, double PGA,
            double Csi_H, double R_g, double T_m)
        {
            InizializzaCoefficienti();

            // STEP 1 → thetaEq, STEP 2 → PsiA/PsiP, STEP 3 → KAEy/KPEy
            EarthPressureSismaResult CSpinteDelTerreno = SpinteDelTerreno.Calculate(
                phiPrime: fi,       // già in radianti
                deltaF:   delta,    // già in radianti
                betaSl:   0,
                ro:       ro,
                H:        H,
                Vs:       Vs,
                PGA:      PGA,
                Csi_H:    Csi_H,
                R_g:      R_g,
                T_m:      T_m
                );

            double KAEy = CSpinteDelTerreno.KAEy;
            double KPEy = CSpinteDelTerreno.KPEy;

            double lunghezza_SpintaAttiva  = T_post[2].y - Muro[11].y;  // Muro[11] = punta-dx dente
            double lunghezza_SpintaPassiva = T_ant[2].y  - Muro[0].y;
        }

        // ── Inizializzazione coefficienti NTC2018 ────────────────────────────
        private void InizializzaCoefficienti()
        {
            // A1 - Tabella 6.2.I NTC2018
            A1_fav["G1"]  = 1.0;
            A1_fav["G2"]  = 0.8;
            A1_fav["Qk1"] = 0.0;
            A1_sfav["G1"]  = 1.3;
            A1_sfav["G2"]  = 1.5;
            A1_sfav["Qk1"] = 1.5;

            // Tabella 6.2.II NTC2018
            M1["TanFi'"]   = 1.0;
            M1["ck'"]      = 1.0;
            M1["Cuk"]      = 1.0;
            M1["Gamma_t'"] = 1.0;

            // Tabella 6.5.I NTC2018
            R3["Grt"]    = 1.4;
            R3["Grs"]    = 1.1;
            R3["GrR"]    = 1.15;
            R3["GrT_v"]  = 1.0;

            VerSismica["G1"]   = 1.0;
            VerSismica["G2"]   = 1.0;
            VerSismica["Qk1"]  = 0.2;
            VerSismica["Qk2"]  = 0.2;
            VerSismica["Grt"]  = 1.2;
            VerSismica["Grs"]  = 1.0;
            VerSismica["GrR"]  = 1.0;
        }

        /// <summary>
        /// Calcola il momento stabilizzante secondo NTC2018 (A1 favorevole e nominale)
        /// </summary>
        public bool MommentoStabilizzante(Punto[] Muro, Punto[] T_ant, Punto[] T_post,
            double Gamma_M, double Gamma_T, double sovraccarico,
            out double StabMin, out double StabNomin)
        {
            double lunghezzaSovraccarico  = T_post[2].x - T_post[3].x;
            double baricentroSovraccarico = (T_post[3].x + T_post[2].x) / 2.0;

            geom.CalcolaAreaEBaricentro(Muro,   out double AreaM,           out Punto baricentroM);
            geom.CalcolaAreaEBaricentro(T_ant,  out double AreaTerrenoAnt,  out Punto BaricentroTerrenoAnt);
            geom.CalcolaAreaEBaricentro(T_post, out double AreaTerrenoPost, out Punto BaricentroTerrenoPost);

            double PesoMuro        = AreaM           * Gamma_M;
            double PesoTerrenoAnt  = AreaTerrenoAnt  * Gamma_T;
            double PesoTerrenoPost = AreaTerrenoPost * Gamma_T;

            double stab_Muro_G1 = PesoMuro * baricentroM.x;
            double stab_Muro_G2 = PesoTerrenoAnt  * BaricentroTerrenoAnt.x
                                 + PesoTerrenoPost * BaricentroTerrenoPost.x;
            double stab_Qk      = lunghezzaSovraccarico * sovraccarico * baricentroSovraccarico;

            double[] MStab = [ stab_Muro_G1, stab_Muro_G2, stab_Qk ];
            StabMin   = Coeff_A1_Fav.Zip(MStab, (c, m) => c * m).Sum();
            StabNomin = stab_Muro_G1 + stab_Muro_G2;

            return true;
        }

        /// <summary>
        /// Calcola il momento ribaltante secondo NTC2018 (A1 sfavorevole e nominale).
        /// KAEy è passato direttamente dal chiamante.
        /// </summary>
        public bool MomentoRibaltamento(Punto[] Muro, Punto[] T_ant, Punto[] T_post, double KAEy,
            double Gamma_M, double Gamma_T, double fi, double delta, double c1, double sovraccarico,
            double ro, double H, double Vs, double PGA, double Csi_H, double R_g, double T_m, double Qf,
            out double RibMax, out double RibNomin)
        {
            // Muro[11] = punta dx dente (inf-dx),  Muro[12] = punta sx dente (inf-sx)
            // Muro[13] = sup-sx dente = piano di posa,  Muro[9] = inf-dx soletta (x=Bf)
            double H_terrenoPosteriore = T_post[2].y - Muro[11].y;   // era Muro[9].y

            double RibG1 = 0.0;

            double RibG2 = KAEy * Gamma_T * Math.Pow(H_terrenoPosteriore, 2) / 2.0;
            RibG2 *= (H_terrenoPosteriore / 3.0 + Muro[11].y);       // era Muro[9].y

            double RibSottospinta = 0.0; // Sottospinta idraulica

            if (Qf < Muro[12].y)                                      // era Muro[10].y
            {
                RibSottospinta = 0.0;
            }

            if (Qf > Muro[12].y)                                      // era Muro[10].y
            {
               RibSottospinta = (Muro[11].x - Muro[12].x) * 10.0     // era Muro[9].x - Muro[10].x
                               * (Qf - Muro[12].y)                    // era Muro[10].y
                               * ((Muro[11].x + Muro[12].x) / 2.0);  // era (Muro[9].x+Muro[10].x)/2
            }
            if (Qf > Muro[13].y & Qf < Muro[0].y)                    // era Muro[11].y
            {
                double larghSottospinta = (Muro[13].x - Muro[0].x) * (Qf - Muro[13].y) / (Muro[0].y - Muro[13].y);
                RibSottospinta += (10 * (Qf - Muro[13].y) * larghSottospinta / 2) * (Muro[13].x - larghSottospinta + larghSottospinta * 2.0 / 3.0);
            }
            if (Qf > Muro[0].y)
            {
                RibSottospinta += 10 * (Muro[0].y - Muro[13].y) * Math.Pow((Muro[13].x - Muro[0].x), 2) / 6.0;
                RibSottospinta += 10 * (Qf - Muro[0].y) * Math.Pow((Muro[13].x + Muro[0].x), 2) / 2.0;
            }

            RibG2 += RibSottospinta;

            double RibQk = KAEy * sovraccarico * H_terrenoPosteriore;
            RibQk *= (H_terrenoPosteriore / 2.0 + Muro[11].y);       // era Muro[9].y

            double[] MRib = [ RibG1, RibG2, RibQk ];
            RibMax    = Coeff_A1_SFav.Zip(MRib, (c, m) => c * m).Sum();
            RibNomin  = RibG1 + RibG2 + RibQk;

            return true;
        }
        public bool SpintaScivolamnto(Punto[] Muro, Punto[] T_ant, Punto[] T_post, double KAEy,
           double Gamma_M, double Gamma_T, double fi, double delta, double c1, double sovraccarico,
           double ro, double H, double Vs, double PGA, double Csi_H, double R_g, double T_m, double Qf,
            out double SpintaScivolamentoG1, out double SpintaScivolamntoG2, out double SpintaScivolamentoQk)
        {
            SpintaScivolamentoG1 = 0.0;
            SpintaScivolamntoG2 = KAEy * Gamma_T * Math.Pow(H, 2) / 2.0;
            if (Qf < Muro[12].y)              // era Muro[10].y (punta sx dente)
            {
                SpintaScivolamntoG2 += 0.0;
            }
            else
            {
                SpintaScivolamntoG2 += (Qf - Muro[12].y) * 10.0 / 2.0;  // era Muro[10].y
            }
            SpintaScivolamentoQk = KAEy * sovraccarico * H;

            return true;
        }

        public bool ResistenzaScivolamnto(Punto[] Muro, Punto[] T_ant, Punto[] T_post, double KAEy,double KPy,
                    double Gamma_M, double Gamma_T, double fi, double delta, double c1, double sovraccarico,
                    double ro, double H, double Vs, double PGA, double Csi_H, double R_g, double T_m, double Qf,
                    out double ResistenzaScivolamentoG1, out double ResistenzaScivolamntoG2, out double ResistenzaScivolamentoQk)
        {
            geom.CalcolaAreaEBaricentro(Muro, out double AreaM, out Punto baricentroM);
            geom.CalcolaAreaEBaricentro(T_ant, out double AreaTerrenoAnt, out Punto BaricentroTerrenoAnt);
            geom.CalcolaAreaEBaricentro(T_post, out double AreaTerrenoPost, out Punto BaricentroTerrenoPost);

            double PesoMuro = AreaM * Gamma_M;

            // Muro[12]=punta-sx dente, Muro[13]=sup-sx dente (piano posa), Muro[11]=punta-dx dente, Muro[9]=inf-dx soletta
            Punto[] MuroImm = new Punto[12];

            if (Qf < Muro[12].y)              // era Muro[10].y
            {
                PesoMuro += 0.0;
            }
            if (Qf > Muro[12].y & Qf <= Muro[13].y)   // era Muro[10].y, Muro[11].y
            {
                MuroImm[0]  = Muro[12];                // era Muro[10]
                MuroImm[1].x = Muro[12].x;             // era Muro[10].x
                MuroImm[1].y = Qf;
                MuroImm[2].x = Muro[11].x;             // era Muro[9].x (lato dx dente)
                MuroImm[2].y = Qf;
                MuroImm[3]  = Muro[11];                // era Muro[9]
                geom.CalcolaAreaEBaricentro(MuroImm, out double AreaMImm, out Punto baricentroMImm);
                PesoMuro -= -10 * AreaMImm;
            }

            if (Qf > Muro[13].y & Qf < Muro[0].y)     // era Muro[11].y
            {
                MuroImm[0] = Muro[12];                 // era Muro[10]
                MuroImm[1] = Muro[13];                 // era Muro[11]
                double larghSottospinta = (Muro[13].x - Muro[0].x) * (Qf - Muro[13].y) / (Muro[0].y - Muro[13].y);
                MuroImm[2].x = Muro[13].x - larghSottospinta;
                MuroImm[2].y = Qf;
                MuroImm[3].x = Muro[11].x;             // era Muro[9].x
                MuroImm[3].y = Qf;
                MuroImm[4] = Muro[11];                 // era Muro[9]
                geom.CalcolaAreaEBaricentro(MuroImm, out double AreaMImm, out Punto baricentroMImm);
                PesoMuro -= -10 * AreaMImm;
            }
            if (Qf > Muro[0].y && Qf < Muro[1].y)
            {
                MuroImm[0] = Muro[12];                 // era Muro[10]
                MuroImm[1] = Muro[13];                 // era Muro[11]
                MuroImm[2] = Muro[0];
                MuroImm[3].x = Muro[0].x;
                MuroImm[3].y = Qf;
                MuroImm[4].x = Muro[11].x;             // era Muro[9].x
                MuroImm[4].y = Qf;
                MuroImm[5] = Muro[11];                 // era Muro[9]
                geom.CalcolaAreaEBaricentro(MuroImm, out double AreaMImm, out Punto baricentroMImm);
                PesoMuro -= -10 * AreaMImm;
            }
            if (Qf > Muro[1].y)
            {
                MuroImm[0] = Muro[12];                 // era Muro[10]
                MuroImm[1] = Muro[13];                 // era Muro[11]
                MuroImm[2] = Muro[0];
                MuroImm[3] = Muro[1];
                MuroImm[4] = Muro[2];
                MuroImm[5].x = Muro[1].x + (Muro[4].x - Muro[1].x) * ((Qf - Muro[1].y) / (Muro[4].y - Muro[1].y));
                MuroImm[5].y = Qf;
                MuroImm[6].x = Muro[7].x - (Muro[7].x - Muro[5].x) * ((Qf - Muro[7].y) / (Muro[5].y - Muro[7].y));
                MuroImm[6].y = Qf;
                MuroImm[7] = Muro[8];
                MuroImm[8] = Muro[11];                 // era Muro[9]
                geom.CalcolaAreaEBaricentro(MuroImm, out double AreaMImm, out Punto baricentroMImm);
                PesoMuro -= -10 * AreaMImm;
            }

            ResistenzaScivolamentoG1 = PesoMuro * Math.Tan(fi);

            ResistenzaScivolamntoG2 = KPy * Gamma_T * Math.Pow((Muro[3].y - Muro[0].y), 2) / 2.0;
            if (Qf < Muro[12].y)             // era Muro[10].y
            {
                ResistenzaScivolamntoG2 += 0.0;
            }
            else
            {
                ResistenzaScivolamntoG2 += (Qf - Muro[12].y) * 10.0 / 2.0;  // era Muro[10].y
            }
            ResistenzaScivolamentoQk = KAEy * sovraccarico * H;

            return true;
        }
    }
}
