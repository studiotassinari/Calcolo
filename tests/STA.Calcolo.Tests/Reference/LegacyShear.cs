// Reference only: TaglioClsRect @ a90d8c939d46e9e8f95de8d25df162489f8a66d8, ClsTaglio.cs.
// Formula code preserved; the MathNet API used here is supplied by LegacyNumerics.cs.
// This source is not part of the shipping application.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaglioClsRect
{
	/// <summary>
	/// Classe per la verifica a taglio di sezioni in calcestruzzo armato.
	/// Implementa le verifiche secondo NTC 2018 e EN 1992-1-1.
	/// </summary>
	class ClsTaglio



	{
		public bool Normativa;

		#region Proprietà Geometriche

		/// <summary>
		/// Altezza totale della sezione [mm]
		/// </summary>
		public double H;

		/// <summary>
		/// Larghezza dell'anima (web) [mm]
		/// </summary>
		public double bw;

		/// <summary>
		/// Ricoprimento delle armature [mm]
		/// </summary>
		public double ric;

		/// <summary>
		/// Altezza utile della sezione [mm]
		/// Calcolata come: d = H - ric
		/// </summary>
		public double d;

		/// <summary>
		/// Area dell'armatura longitudinale tesa [mm²]
		/// </summary>
		public double Asl;

		#endregion

		#region Proprietà dei Materiali

		/// <summary>
		/// Resistenza caratteristica a compressione del calcestruzzo [MPa]
		/// </summary>
		public double fck;

		/// <summary>
		/// Resistenza di calcolo a compressione del calcestruzzo [MPa]
		/// Calcolata come: fcd = fck * acc / gc
		/// </summary>
		public double fcd;

		/// <summary>
		/// Resistenza caratteristica a snervamento dell'acciaio [MPa]
		/// </summary>
		public double fyk;

		/// <summary>
		/// Resistenza di calcolo a snervamento dell'acciaio [MPa]
		/// Calcolata come: fyd = fyk / ga
		/// </summary>
		public double fyd;

		/// <summary>
		/// Coefficiente parziale di sicurezza del calcestruzzo [-]
		/// Generalmente: gc = 1.50
		/// </summary>
		public double gc;

		/// <summary>
		/// Coefficiente parziale di sicurezza dell'acciaio [-]
		/// Generalmente: ga = 1.15
		/// </summary>
		public double ga;

		/// <summary>
		/// Coefficiente riduttivo per resistenza a compressione (alfa cc) [-]
		/// NTC 2018: acc = 0.85
		/// EN 1992-1-1: acc = 1.00
		/// </summary>
		public double acc;

		#endregion

		#region Proprietà delle Sollecitazioni

		/// <summary>
		/// Sforzo normale di compressione [N]
		/// Positivo se compressione
		/// </summary>
		public double Ned;

		/// <summary>
		/// Taglio di progetto [kN]
		/// </summary>
		public double Ved;

		/// <summary>
		/// Tensione media di compressione [MPa]
		/// Calcolata come: Scp = Ned / (bw * d) o Ned / Ac
		/// </summary>
		public double Scp;

		#endregion

		#region Proprietà delle Armature a Taglio

		/// <summary>
		/// Diametro delle staffe [mm]
		/// </summary>
		public double Dia;

        /// <summary>
        /// Area della sezione trasversale delle staffe [mm²]
        /// Calcolata come: Asw = nBr * π * Dia² / 4
        /// </summary>
        public double Asw;
		//public double AswPieg;

        /// <summary>
        /// Interasse delle staffe [mm]
        /// </summary>
        public double IntSt;

        /// <summary>
        /// Numero di bracci delle staffe [-]
        /// </summary>
        public double nBr;	

        /// <summary>
        /// Inclinazione delle staffe rispetto all'asse della trave [gradi]
        /// Generalmente: Alfa = 90° (staffe verticali)
        /// </summary>
        public double Alfa;

        /// <summary>
        /// Inclinazione delle bielle compresse rispetto all'asse della trave [gradi]
        /// Variabile tra 21.8° e 45° (EN 1992) o 22.5° e 45° (NTC 2018)
        /// </summary>
        public double Theta;

		/// <summary>
		/// Diametro dei ferri piegati [mm]
		/// Ferri longitudinali inclinati che contribuiscono alla resistenza a taglio
		/// Ferri longitudinali inclinati che contribuiscono alla resistenza a taglio
		/// </summary>
		public double DiaPieg;

		/// <summary>
		/// Interasse dei ferri piegati [mm]
		/// </summary>
		public double IntPieg;

		/// <summary>
		/// Inclinazione dei ferri piegati rispetto all'asse della trave [gradi]
		/// </summary>
		public double AlfaPieg;

		/// <summary>
		/// Numero di ferri piegati [-]
		/// </summary>
		public double nPieg;

		#endregion

		#region Risultati dei Calcoli

		/// <summary>
		/// Resistenza a taglio della sezione SENZA armature a taglio [N]
		/// Calcolata secondo § 4.1.2.1.3.1 NTC 2018
		/// </summary>
		public double VRd;

		/// <summary>
		/// Resistenza a taglio delle STAFFE [kN]
		/// Calcolata secondo § 4.1.2.1.3.2 NTC 2018
		/// </summary>
		public double VRst;

		/// <summary>
		/// Resistenza a taglio delle BIELLE compresse [kN]
		/// Calcolata secondo § 4.1.2.1.3.2 NTC 2018
		/// </summary>
		public double VRbiella;

		/// <summary>
		/// Coefficiente di sfruttamento delle staffe [-]
		/// Calcolato come: Ved / VRst
		/// Deve essere ≤ 1.0
		/// </summary>
		public double Sfruttamento_Staffe;

		#endregion

		#region Parametri Adimensionali

		/// <summary>
		/// Percentuale geometrica di armatura longitudinale [-]
		/// Calcolata come: ro_l = Asl / (bw * d)
		/// Limitata a: ro_l ≤ 0.02
		/// </summary>
		public double ro_l;

		/// <summary>
		/// Coefficiente dipendente dall'altezza utile [-]
		/// Calcolato come: k = 1 + √(200/d) ≤ 2.0
		/// </summary>
		public double k;

		/// <summary>
		/// Resistenza minima a taglio [MPa]
		/// Calcolata come: vmin = 0.035 * k^(3/2) * √fck
		/// </summary>
		public double vmin;

		/// <summary>
		/// Coefficiente riduttivo per stati di compressione [-]
		/// Funzione di Scp/fcd secondo § 4.1.2.1.3.2 NTC 2018
		/// </summary>
		public double acw;

		#endregion

		#region Costruttore

		/// <summary>
		/// Costruttore della classe ClsTaglio
		/// Inizializza la resistenza di calcolo dell'acciaio
		/// </summary>
		public ClsTaglio()
		{
			fyd = fyk / ga;
		}

		#endregion

		#region Metodi Statici di Utilità

		/// <summary>
		/// Calcola la cotangente di un angolo espresso in gradi
		/// </summary>
		/// <param name="x">Angolo in gradi</param>
		/// <returns>Cotangente dell'angolo</returns>
		public static double cot(double x)
		{
			double xR = Trig.DegreeToRadian(x);
			return Trig.Cot(xR);
		}

		public static double sin(double x)
		{
			double xR = Trig.DegreeToRadian(x);
			return Trig.Sin(xR);
        }
        public static double cos(double x)
        {
            double xR = Trig.DegreeToRadian(x);
            return Trig.Cos(xR);
        }

		public static double tan(double x)
		{
            double xR = Trig.DegreeToRadian(x);
            return Trig.Tan(x); 
		}


        #endregion

        #region Calcolo Resistenza SENZA Armature a Taglio

        /// <summary>
        /// Calcola la resistenza a taglio della sezione SENZA armature a taglio.
        /// Riferimento: § 4.1.2.1.3.1 NTC 2018 / § 6.2.2 EN 1992-1-1
        /// Formula: VRd = [0.18/gc * k * (100*ro_l*fck)^(1/3) + 0.15*scp] * bw * d
        /// </summary>
        public void VRD()
		{
			// Calcolo altezza utile
			d = H - ric;

			// Calcolo coefficiente k (effetto scala)
			k = 1.0 + Math.Pow((200.0 / d), 0.5);
			if (k > 2.0)
			{
				k = 2.0; // Limitazione superiore
			}

			// Calcolo resistenza minima
			vmin = 0.035 * Math.Pow(k, (3.0 / 2.0)) * Math.Pow(fck, 0.5);

			// Calcolo percentuale geometrica di armatura longitudinale
			ro_l = Asl / (d * bw);
			if (ro_l > 0.02)
			{
				ro_l = 0.02; // Limitazione superiore
			}

			// Calcolo tensione media di compressione
			Scp = Ned / (d * bw);
			if (Scp > (0.2 * fcd))
			{
				Scp = 0.2 * fcd; // Limitazione superiore
			}

			// Formula principale per VRd
			VRd = ((0.18 * k * Math.Pow(100 * ro_l * fck, 1.0 / 3.0)) / gc + 0.15 * Scp) * bw * d;

			// Verifica con resistenza minima
			double Vrd1 = (vmin + 0.15 * Scp) * bw * d;
			if (Vrd1 > VRd)
			{
				VRd = Vrd1; // Assume il valore maggiore
			}
		}

		#endregion

		#region   Calcolo Resistenza Staffe e piegati

		/// <summary>
		/// Calcola la resistenza a taglio fornita dalle STAFFE.
		/// Riferimento: § 4.1.2.1.3.2 NTC 2018 / § 6.2.3 EN 1992-1-1
		/// Formula: VRsd = 0.9 * d * (Asw/s) * fyd * (cotθ + cotα) * sinα
		/// </summary>
		public void Vr_staffe()
		{
			double t = Theta;	
			fyd = fyk / ga;
			d = H - ric;
			
			double AswPieg = nPieg * Math.PI * Math.Pow(DiaPieg, 2) / 4; // Area ferri piegati
                                                                         // Calcolo area delle staffe (n bracci)
            Asw = nBr * Math.PI * Math.Pow(Dia, 2) / 4;

	
			// Formula della resistenza delle staffe e piegati (risultato in kN)
			VRst = (1 / 1000.00) * (0.9 * d * Asw * fyd / IntSt) * (cot(t)*sin(Alfa) + cos(Alfa));
            VRst= VRst+(1 / 1000.00) * (0.9 * d * AswPieg * fyd / IntPieg) * (cot(t)*sin(AlfaPieg) + cos(AlfaPieg));
		}

		/// <summary>
		/// Calcola la resistenza a taglio fornita dalle STAFFE con angolo Theta specificato.
		/// </summary>
		/// <param name="t">Angolo Theta in gradi</param>
		/// <returns>Resistenza a taglio delle staffe [kN]</returns>
		public double Vr_staffe(double t)
		{
			fyd = fyk / ga;
			d = H - ric;

			// Calcolo area  staffe e piegati (n bracci)
			Asw = nBr * Math.PI * Math.Pow(Dia, 2) / 4;
            double AswPieg = nPieg * Math.PI * Math.Pow(DiaPieg, 2) / 4; // Area ferri piegati


            // Formula della resistenza staffe e piegati (risultato in kN)
            VRst = (1 / 1000.00) * (0.9 * d * Asw * fyd / IntSt) * (cot(t) * sin(Alfa) + cos(Alfa));
            VRst = VRst + (1 / 1000.00) * (0.9 * d * AswPieg * fyd / IntPieg) * (cot(t) * sin(AlfaPieg) + cos(AlfaPieg));

            return VRst;
		}

        #endregion
   

        #region Calcolo Resistenza Bielle (NTC 2018)

        /// <summary>
        /// Calcola la resistenza a taglio delle BIELLE COMPRESSE secondo NTC 2018.
        /// Riferimento: § 4.1.2.1.3.2 NTC 2018
        /// Formula: VRcd = 0.9 * d * bw * acw * fcd * v1 * (cotθ + cotα) / (1 + cot²θ)
        /// con v1 = 0.5 (NTC 2018)
        /// </summary>
        public void Vr_biella()
		{
			double t = Theta;
			fcd = fck * acc / gc;
			d = H - ric;

			// Calcolo area del calcestruzzo
			double Ac = bw * H;
			double Ac_om = Ac + 15 * Asl; // Area omogeneizzata (non utilizzata)

			// Calcolo tensione media di compressione
			Scp = Ned / Ac;

			// Calcolo coefficiente acw in funzione del livello di compressione
			// § 4.1.2.1.3.2 NTC 2018
			acw = 1.0; // Valore di default

			if ((Scp <= 0.25 * fcd) && (Scp > 0.0))
			{
				acw = (1 + Scp / fcd); // 0 < σcp ≤ 0.25 fcd
			}
			else if ((Scp <= 0.50 * fcd) && (Scp > 0.25 * fcd))
			{
				acw = 1.25; // 0.25 fcd < σcp ≤ 0.50 fcd
			}
			else if ((Scp <= 1.0 * fcd) && (Scp > 0.5 * fcd))
			{
				acw = 2.5 * (1 - Scp / fcd); // 0.50 fcd < σcp ≤ 1.0 fcd
			}
			else if (Scp > fcd)
			{
				acw = 0; // σcp > fcd (condizione anomala)
			}

			// Coefficiente di riduzione della resistenza (NTC 2018)
			double v1 = 0.5;

			// Calcolo della resistenza delle bielle (risultato in kN)
			double V1 = (acw * bw * 0.9 * d * fcd * v1);
			double V2 = cot(t) + cot(Alfa);
			double V3 = 1 + Math.Pow(cot(t), 2);

			VRbiella = (V1 * V2 / V3) / 1000.0;
		}

		/// <summary>
		/// Calcola la resistenza a taglio delle BIELLE COMPRESSE secondo NTC 2018 con angolo Theta specificato.
		/// </summary>
		/// <param name="t">Angolo Theta in gradi</param>
		/// <returns>Resistenza a taglio delle bielle [kN]</returns>
		public double Vr_biella(double t)
		{
			fcd = fck * acc / gc;
			d = H - ric;

			// Calcolo area del calcestruzzo
			double Ac = bw * H;
			double Ac_om = Ac + 15 * Asl; // Area omogeneizzata (non utilizzata)

			// Calcolo tensione media di compressione
			Scp = Ned / Ac;

			// Calcolo coefficiente acw in funzione del livello di compressione
			acw = 1.0;

			if ((Scp <= 0.25 * fcd) && (Scp > 0.0))
			{
				acw = (1 + Scp / fcd);
			}
			else if ((Scp <= 0.50 * fcd) && (Scp > 0.25 * fcd))
			{
				acw = 1.25;
			}
			else if ((Scp <= 1.0 * fcd) && (Scp > 0.5 * fcd))
			{
				acw = 2.5 * (1 - Scp / fcd);
			}
			else if (Scp > fcd)
			{
				acw = 0;
			}

			// Coefficiente di riduzione della resistenza (NTC 2018)
			double v1 = 0.5;

			// Calcolo della resistenza delle bielle (risultato in kN)
			double V1 = (acw * bw * 0.9 * d * fcd * v1);
			double V2 = cot(t) + cot(Alfa);
			double V3 = 1 + Math.Pow(cot(t), 2);

			VRbiella = (V1 * V2 / V3) / 1000.0;

			return VRbiella;
		}

		#endregion

		#region Calcolo Resistenza Bielle (EN 1992-1-1)

		/// <summary>
		/// Calcola la resistenza a taglio delle BIELLE COMPRESSE secondo EN 1992-1-1.
		/// Riferimento: § 6.2.3 EN 1992-1-1
		/// Formula: VRcd = 0.9 * d * bw * acw * fcd * v1 * (cotθ + cotα) / (1 + cot²θ)
		/// con v1 = 0.6 * (1 - fck/250) per fck > 60 MPa (Eurocodice)
		/// </summary>
		/// 

		public void Vr_biella1992()
		{
			double t = Theta;
			fcd = fck * acc / gc;
			d = H - ric;

			// Calcolo area del calcestruzzo
			double Ac = bw * H;
			double Ac_om = Ac + 15 * Asl; // Area omogeneizzata (non utilizzata)

			// Calcolo tensione media di compressione
			Scp = Ned / Ac;

			// Calcolo coefficiente acw in funzione del livello di compressione
			acw = 1.0;

			if ((Scp <= 0.25 * fcd) && (Scp > 0.0))
			{
				acw = (1 + Scp / fcd);
			}
			else if ((Scp <= 0.50 * fcd) && (Scp > 0.25 * fcd))
			{
				acw = 1.25;
			}
			else if ((Scp <= 1.0 * fcd) && (Scp > 0.5 * fcd))
			{
				acw = 2.5 * (1 - Scp / fcd);
			}
			else if (Scp > fcd)
			{
				acw = 0;
			}

			// Coefficiente di riduzione della resistenza (EN 1992-1-1)
			// v1 = 0.6 * (1 - fck/250) per calcestruzzi con fck > 60 MPa
			double v1 = 0.6;
			if (fck > 60)
			{
				v1 = 0.9 - fck / 200;
			}
			if (v1 < 0.5)
			{
				v1 = 0.5; // Limitazione inferiore
			}

			// Calcolo della resistenza delle bielle (risultato in kN)
			double V1 = (acw * bw * 0.9 * d * fcd * v1);
			double V2 = cot(t) + cot(Alfa);
			double V3 = 1 + Math.Pow(cot(t), 2);

			VRbiella = (V1 * V2 / V3) / 1000.0;
		}

		/// <summary>
		/// Calcola la resistenza a taglio delle BIELLE COMPRESSE secondo EN 1992-1-1 con angolo Theta specificato.
		/// </summary>
		/// <param name="t">Angolo Theta in gradi</param>
		/// <returns>Resistenza a taglio delle bielle [kN]</returns>
		public double Vr_biella1992(double t)
		{
			fcd = fck * acc / gc;
			d = H - ric;

			// Calcolo area del calcestruzzo
			double Ac = bw * H;
			double Ac_om = Ac + 15 * Asl; // Area omogeneizzata (non utilizzata)

			// Calcolo tensione media di compressione
			Scp = Ned / Ac;

			// Calcolo coefficiente acw in funzione del livello di compressione
			acw = 1.0;

			if ((Scp <= 0.25 * fcd) && (Scp > 0.0))
			{
				acw = (1 + Scp / fcd);
			}
			else if ((Scp <= 0.50 * fcd) && (Scp > 0.25 * fcd))
			{
				acw = 1.25;
			}
			else if ((Scp <= 1.0 * fcd) && (Scp > 0.5 * fcd))
			{
				acw = 2.5 * (1 - Scp / fcd);
			}
			else if (Scp > fcd)
			{
				acw = 0;
			}

			// Coefficiente di riduzione della resistenza (EN 1992-1-1)
			double v1 = 0.6;
			if (fck > 60)
			{
				v1 = 0.9 - fck / 200;
			}
			if (v1 < 0.5)
			{
				v1 = 0.5;
			}

			// Calcolo della resistenza delle bielle (risultato in kN)
			double V1 = (acw * bw * 0.9 * d * fcd * v1);
			double V2 = cot(t) + cot(Alfa);
			double V3 = 1 + Math.Pow(cot(t), 2);

			VRbiella = (V1 * V2 / V3) / 1000.0;

			return VRbiella;
		}

        #endregion

        #region Calcolo Angolo Theta Ottimale NTC 2018

        /// <summary>
        /// Calcola l'angolo Theta ottimale per massimizzare la resistenza a taglio.
        /// L'angolo ottimale è quello per cui VRbiella = VRstaffe.
        /// Ricerca nell'intervallo 22.5° ≤ θ ≤ 45° mediante metodo di bisezione.
        /// </summary>
        /// <returns>Angolo Theta ottimale [gradi]</returns>
        public double Theta_Ott()
		{
			// Funzione obiettivo: differenza tra resistenza bielle e staffe

		
			Func<double, double> f = x => (Vr_biella(x) - Vr_staffe(x));

			double Tott = 45; // Valore di default

			// Verifica casi limite
			if (Vr_staffe(22.5) <= Vr_biella(22.5))
			{
				Tott = 22.5; // Condizionato da staffe al limite inferiore
			}
			else if (Vr_staffe(45) >= Vr_biella(45))
			{
				Tott = 45; // Condizionato da bielle al limite superiore
			}
			else
			{
				// Ricerca della radice con metodo di bisezione
				Tott = Bisection.FindRoot(f, 22.5, 45, 0.000001);
			}

			return Tott;
		}

		/// <summary>
		/// Calcola l'angolo Theta ottimale con coefficiente riduttivo (per pareti).
		/// </summary>
		/// <param name="coeff">Coefficiente riduttivo da applicare alla resistenza delle bielle (tipicamente 0.4 per pareti)</param>
		/// <returns>Angolo Theta ottimale [gradi]</returns>
		public double Theta_Ott(double coeff)
		{
			// Funzione obiettivo con coefficiente riduttivo
			Func<double, double> f1 = x => (Vr_biella(x) * coeff - Vr_staffe(x));

			double Tott = 45; // Valore di default

			// Verifica casi limite
			if (Vr_staffe(22.5) <= Vr_biella(22.5) * coeff)
			{
				Tott = 22.5;
			}
			else if (Vr_staffe(45) >= Vr_biella(45) * coeff)
			{
				Tott = 45;
			}
			else
			{
				// Ricerca della radice con metodo di bisezione
				Tott = Bisection.FindRoot(f1, 22.5, 45, 0.000001);
			}

			return Tott;
		}

        #endregion

        #region Calcolo Angolo Theta Ottimale EN 1992-1-1

        /// <summary>
        /// Calcola l'angolo Theta ottimale secondo EN 1992-1-1.
        /// Metodo specifico per la normativa europea.
        /// </summary>
        /// <returns>Angolo Theta ottimale [gradi]</returns>
        public double Theta_Ott_EN_1992_1_1()
        {
            // Funzione obiettivo usando la formula EN 1992
            Func<double, double> f = x => (Vr_biella1992(x) - Vr_staffe(x));

            double Tott = 45; // Valore di default

            // Calcola le resistenze ai limiti dell'intervallo
            double VRbiella_22_5 = Vr_biella1992(22.5);
            double VRbiella_45 = Vr_biella1992(45);
            double VRstaffe_22_5 = Vr_staffe(22.5);
            double VRstaffe_45 = Vr_staffe(45);

            // Verifica casi limite
            if (VRstaffe_22_5 <= VRbiella_22_5)
            {
                Tott = 22.5; // Condizionato da staffe al limite inferiore
            }
            else if (VRstaffe_45 >= VRbiella_45)
            {
                Tott = 45; // Condizionato da bielle al limite superiore
            }
            else
            {
                // Ricerca della radice con metodo di bisezione
                Tott = Bisection.FindRoot(f, 22.5, 45, 0.000001);
            }

            return Tott;
        }

        /// <summary>
        /// Calcola l'angolo Theta ottimale con coefficiente riduttivo (per pareti).
        /// </summary>
        /// <param name="coeff">Coefficiente riduttivo da applicare alla resistenza delle bielle (tipicamente 0.4 per pareti)</param>
        /// <returns>Angolo Theta ottimale [gradi]</returns>
        public double Theta_Ott_EN_1992_1_1(double coeff)
        {
            // Funzione obiettivo con coefficiente riduttivo
            Func<double, double> f1 = x => (Vr_biella1992(x) * coeff - Vr_staffe(x));

            double Tott = 45; // Valore di default

            // Verifica casi limite
            if (Vr_staffe(22.5) <= Vr_biella1992(22.5) * coeff)
            {
                Tott = 22.5;
            }
            else if (Vr_staffe(45) >= Vr_biella1992(45) * coeff)
            {
                Tott = 45;
            }
            else
            {
                // Ricerca della radice con metodo di bisezione
                Tott = Bisection.FindRoot(f1, 22.5, 45, 0.000001);
            }

            return Tott;
        }

        #endregion

    }
}

