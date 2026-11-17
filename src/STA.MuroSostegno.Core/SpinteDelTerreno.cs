// Motore importato da TA-MuroDiSostegno: formule conservate per confronto con il programma originale.
// Limiti noti e validazione documentati in docs/MURO-SOSTEGNO.md.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STA.MuroSostegno.Core.Legacy
{

    /// <summary>
    /// Classe per il calcolo delle spinte del terreno su strutture di sostegno.
    /// Implementa le formule secondo la teoria di Coulomb modificata per terreni incoerenti.
    /// Calcola coefficienti di spinta attiva (KAEy) e passiva (KPEy) e gli angoli critici (ψA, ψP).
    /// </summary>
    internal class SpinteDelTerreno

    {
        private double _tolerance;
private double _safetyFactor;

public SpinteDelTerreno(double tolerance = 1e-12, double safetyFactor = 1.0)
{
    _tolerance = tolerance;
    _safetyFactor = safetyFactor;
}
        #region Costanti

        /// <summary>
        /// Tolleranza numerica per confronti floating-point e validazioni
        /// </summary>
        private const double Tolerance = 1e-12;

        #endregion

        #region   Coefficienti


        /// <summary>
        /// Collico i coefficinti per la spinta attiva e passiva
        /// in sona sismica.
        /// </summary>
        // (Qui puoi aggiungere i metodi o le proprietà necessari)
        #endregion

        #region SPINTA ATTIVA E PASSIVA

        /// <summary>
        /// Struttura che contiene i risultati del calcolo della spinta del terreno
        /// </summary>
        public struct EarthPressureSismaResult
        {
            public double KAEy { get; }
            public double KPEy { get; }
            public double PsiA { get; }
            public double PsiP { get; }
            public double AlfaH { get; }
            public double TethaEq { get; }

            public EarthPressureSismaResult(double kAEy, double kPEy, double psiA, double psiP, double alfaH, double tethaEq)
            {
                KAEy = kAEy;
                KPEy = kPEy;
                PsiA = psiA;
                PsiP = psiP;
                AlfaH = alfaH;
                TethaEq = tethaEq;
            }
        }

        public static double AlfaH(double ro, double H, double Vs, double PGA,
            double Csi_H, double R_g, double T_m,
            out double B_H, out double alfaH, out double tethaEq)
        {
            double G = R_g * ro * Vs * Vs;
            B_H = 1.0;

            double Ord = H / (T_m * Math.Sqrt(G / ro));

            if (Ord <= 0.05)
                B_H = 1.0;
            else if (Ord >= 0.4)
                B_H = 0.4;
            else // Ord > 0.05 && Ord < 0.4
                B_H = 1.0 - ((1.0 - 0.35) / (0.4 - 0.05)) * (Ord - 0.05);

            alfaH = B_H * PGA / Csi_H;
            tethaEq = Math.Atan(alfaH);

            return alfaH;
        }

        /// <summary>
        /// Calcola tutti i coefficienti di spinta nell'ordine corretto:
        /// 1. AlfaH → thetaEq
        /// 2. PsiA, PsiP (dipendono da thetaEq)
        /// 3. KAEy, KPEy (dipendono da thetaEq, PsiA, PsiP)
        /// </summary>
        public static EarthPressureSismaResult Calculate(
            double phiPrime,    // φ' angolo di attrito interno terreno [rad]
            double deltaF,      // δf angolo di attrito muro-terreno [rad]
            double betaSl,      // βsl pendenza del terrapieno [rad]
            double ro,          // ρ densità del terreno [kg/m³]
            double H,           // H altezza del muro [m]
            double Vs,          // Vs velocità onde di taglio [m/s]
            double PGA,         // PGA accelerazione al picco del suolo [g]
            double Csi_H,       // χ coefficiente di riduzione
            double R_g,         // rapporto riduzione modulo G
            double T_m)
            //double PsiSovracc)         // Tm periodo rappresentativo [s]
        {
            // STEP 1: calcola thetaEq tramite AlfaH
            AlfaH(ro, H, Vs, PGA, Csi_H, R_g, T_m,
                out double B_H,
                out double alfaHValue,
                out double thetaEq);

            // STEP 2: calcola gli angoli critici (dipendono da thetaEq)
            double psiA = CalculatePsiA(phiPrime, deltaF, betaSl, thetaEq);
            double psiP = CalculatePsiP(phiPrime, deltaF, betaSl, thetaEq);

            // STEP 3: calcola i coefficienti di spinta (dipendono da thetaEq, psiA, psiP)
            double kAEy = CalculateKAEy(phiPrime, deltaF, betaSl, thetaEq, psiA);
            double kPEy = CalculateKPEy(phiPrime, deltaF, betaSl, thetaEq, psiP);

            return new EarthPressureSismaResult(kAEy, kPEy, psiA, psiP, alfaHValue, thetaEq);
        }

        /// <summary>
        /// Formula (F.1)
        /// </summary>
        public static double CalculateKAEy(
            double phiPrime,
            double deltaF,
            double betaSl,
            double thetaEq,
            double? psiAOverride = null)
        {
            double psiA = psiAOverride ?? CalculatePsiA(phiPrime, deltaF, betaSl, thetaEq);

            double term1Den =
                Math.Cos(betaSl + thetaEq) +
                SafeSqrt(Sin2(phiPrime) - Sin2(betaSl + thetaEq));

            double term2 =
                Math.Cos(deltaF) -
                SafeSqrt(Sin2(phiPrime) - Sin2(deltaF));

            double bracket = (Math.Cos(deltaF) / term1Den) * term2;

            double ratio = Math.Cos(betaSl) / SafeDivideDenominator(Math.Cos(thetaEq));
            double expTerm = Math.Exp(-2.0 * psiA * Math.Tan(phiPrime));

            return bracket * ratio * expTerm;
        }

        /// <summary>
        /// Formula (F.2)
        /// </summary>
        public static double CalculateKPEy(
            double phiPrime,
            double deltaF,
            double betaSl,
            double thetaEq,
            double? psiPOverride = null)
        {
            double psiP = psiPOverride ?? CalculatePsiP(phiPrime, deltaF, betaSl, thetaEq);

            double term1Den =
                Math.Cos(betaSl - thetaEq) -
                SafeSqrt(Sin2(phiPrime) - Sin2(betaSl - thetaEq));

            double term2 =
                Math.Cos(deltaF) +
                SafeSqrt(Sin2(phiPrime) - Sin2(deltaF));

            double bracket = (Math.Cos(deltaF) / term1Den) * term2;

            double ratio = Math.Cos(betaSl) / SafeDivideDenominator(Math.Cos(thetaEq));
            double expTerm = Math.Exp(+2.0 * psiP * Math.Tan(phiPrime));

            return bracket * ratio * expTerm;
        }

        /// <summary>
        /// Formula (F.3)
        /// ψA = 0.5 * { asin[sin(δf)/sin(φ')] - asin[sin(βsl + θeq)/sin(φ')] - δf + βsl - θeq }
        /// </summary>
        public static double CalculatePsiA(
            double phiPrime,
            double deltaF,
            double betaSl,
            double thetaEq)
        {
            double a = SafeAsin(Math.Sin(deltaF) / SafeDivideDenominator(Math.Sin(phiPrime)));
            double b = SafeAsin(Math.Sin(betaSl + thetaEq) / SafeDivideDenominator(Math.Sin(phiPrime)));

            return 0.5 * (a - b - deltaF + betaSl - thetaEq);
        }

        /// <summary>
        /// Formula (F.4)
        /// ψP = 0.5 * { asin[sin(δf)/sin(φ')] + asin[sin(βsl - θeq)/sin(φ')] + δf + βsl + θeq }
        /// </summary>
        public static double CalculatePsiP(
            double phiPrime,
            double deltaF,
            double betaSl,
            double thetaEq)
        {
            double a = SafeAsin(Math.Sin(deltaF) / SafeDivideDenominator(Math.Sin(phiPrime)));
            double b = SafeAsin(Math.Sin(betaSl - thetaEq) / SafeDivideDenominator(Math.Sin(phiPrime)));

            return 0.5 * (a + b + deltaF + betaSl + thetaEq);
        }

        private static double Sin2(double x)
        {
            double s = Math.Sin(x);
            return s * s;
        }

        private static double SafeSqrt(double value)
        {
            if (value < -Tolerance)
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Radicando negativo: {value}");

            return Math.Sqrt(Math.Max(0.0, value));
        }

        private static double SafeAsin(double value)
        {
            if (value < -1.0 - Tolerance || value > 1.0 + Tolerance)
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Argomento asin fuori dominio: {value}");

            return Math.Asin(Clamp(value, -1.0, 1.0));
        }

        private static double SafeDivideDenominator(double denominator)
        {
            if (Math.Abs(denominator) < Tolerance)
                throw new DivideByZeroException("Denominatore nullo o troppo vicino a zero.");

            return denominator;
        }

        /// <summary>
        /// Implementazione di Math.Clamp per compatibilità con .NET Framework 4.8
        /// </summary>
        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
#endregion