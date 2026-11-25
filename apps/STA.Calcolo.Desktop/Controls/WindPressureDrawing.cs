using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Vento.Aerodinamica;

namespace STA.Calcolo.Desktop.Controls
{
    /// <summary>
    /// Figura dei risultati: sezione nella direzione del vento con le pressioni disegnate sulle superfici.
    /// Ogni zona della copertura è una fascia normale al profilo, di spessore proporzionale a |p|, estesa
    /// sul tratto che la zona occupa; sotto la sezione una linea di quota riporta le ascisse x dei confini
    /// fra le zone, misurate dal bordo sopravento. Le frecce puntano verso la superficie se p &gt; 0
    /// (pressione) e se ne allontanano se p &lt; 0 (depressione). Elemento di sola presentazione:
    /// riceve le righe già calcolate e non contiene formule.
    /// </summary>
    public sealed class WindPressureDrawing : WindBuildingDrawing
    {
        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(IReadOnlyList<AzioneSuperficie>),
            typeof(WindPressureDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string),
            typeof(WindPressureDrawing), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Azioni da disegnare: una sola condizione di carico per superficie</summary>
        public IReadOnlyList<AzioneSuperficie>? Rows
        {
            get { return (IReadOnlyList<AzioneSuperficie>?)GetValue(RowsProperty); }
            set { SetValue(RowsProperty, value); }
        }

        /// <summary>Condizione di carico rappresentata, scritta accanto al titolo</summary>
        public string Caption
        {
            get { return (string)GetValue(CaptionProperty); }
            set { SetValue(CaptionProperty, value); }
        }

        // Depressione in petrolio, pressione in ocra: gli stessi accenti del tema.
        private static readonly Brush Depressione = Congela(new SolidColorBrush(Color.FromArgb(0x50, 0x13, 0x5F, 0x6E)));
        private static readonly Brush Pressione = Congela(new SolidColorBrush(Color.FromArgb(0x66, 0xC3, 0x87, 0x2A)));
        private static readonly Pen PennaDepressione = Congela(new Pen(new SolidColorBrush(Color.FromRgb(0x13, 0x5F, 0x6E)), 1.3));
        private static readonly Pen PennaPressione = Congela(new Pen(new SolidColorBrush(Color.FromRgb(0xA3, 0x5B, 0x18)), 1.3));
        private static readonly Brush TestoDepressione = Congela(new SolidColorBrush(Color.FromRgb(0x13, 0x5F, 0x6E)));
        private static readonly Brush TestoPressione = Congela(new SolidColorBrush(Color.FromRgb(0xA3, 0x5B, 0x18)));
        private const double SpessoreMassimo = 46;

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRoundedRectangle(Sfondo, Bordo, new Rect(0.5, 0.5, Math.Max(ActualWidth - 1, 0), Math.Max(ActualHeight - 1, 0)), 4, 4);
            if (ActualWidth < 320 || ActualHeight < 180) return;

            IReadOnlyList<AzioneSuperficie> righe = Rows ?? Array.Empty<AzioneSuperficie>();
            if (righe.Count == 0 || !(B > 0) || !(D > 0) || !(H > 0))
            {
                Scrivi(dc, "Nessun risultato da rappresentare", new Point(ActualWidth / 2, ActualHeight / 2), Testo, 12, false, 0.5, 0.5);
                return;
            }

            // Con vento parallelo al colmo la sezione nella direzione del vento taglia la copertura lungo una linea orizzontale.
            List<Point> profilo;
            double sommità;
            if (VentoParallelo && !ÈVolta)
            {
                sommità = H;
                profilo = new List<Point> { new Point(0, H), new Point(D, H) };
            }
            else
            {
                profilo = ProfiloCopertura(D, out sommità);
            }

            const double margineSinistro = 150, margineDestro = 150, margineAlto = 92, margineBasso = 66;
            double disponibileX = ActualWidth - margineSinistro - margineDestro;
            double disponibileY = ActualHeight - margineAlto - margineBasso;
            if (disponibileX < 60 || disponibileY < 40) return;

            // stesse proporzioni limitate dello schema quotato, così le due figure si corrispondono
            double rapporto = Math.Clamp(sommità / D, 0.22, 1.5);
            double scalaX = Math.Min(disponibileX / D, disponibileY / (rapporto * D));
            double scalaY = scalaX * rapporto * D / sommità;
            double origineX = margineSinistro + (disponibileX - D * scalaX) / 2;
            double origineY = ActualHeight - margineBasso;
            Point P(double x, double y) => new Point(origineX + x * scalaX, origineY - y * scalaY);

            Scrivi(dc, "Pressioni sulla sezione nella direzione del vento [N/m²]", new Point(12, 9), TestoTitolo, 12, false, 0, 0);
            if (!string.IsNullOrEmpty(Caption)) Scrivi(dc, Caption, new Point(12, 26), Testo, 11, false, 0, 0);
            Legenda(dc);

            // terreno e costruzione
            double x0 = P(0, 0).X, x1 = P(D, 0).X;
            dc.DrawLine(Terreno, new Point(x0 - 30, origineY + 2.5), new Point(x1 + 30, origineY + 2.5));
            bool tettoia = righe.Any(r => r.Simbolo == "cF");
            if (tettoia)
            {
                dc.DrawLine(Tratteggio, P(0, 0), P(0, profilo[0].Y));
                dc.DrawLine(Tratteggio, P(D, 0), P(D, profilo[^1].Y));
                for (int i = 1; i < profilo.Count; i++) dc.DrawLine(Contorno, P(profilo[i - 1].X, profilo[i - 1].Y), P(profilo[i].X, profilo[i].Y));
            }
            else
            {
                var figura = new StreamGeometry();
                using (StreamGeometryContext g = figura.Open())
                {
                    g.BeginFigure(P(0, 0), true, true);
                    foreach (Point p in profilo) g.LineTo(P(p.X, p.Y), true, false);
                    g.LineTo(P(D, 0), true, false);
                }
                figura.Freeze();
                dc.DrawGeometry(Riempimento, Contorno, figura);
            }

            double massimo = righe.Where(r => r.Parte != ParteCostruzione.PareteLaterale && r.Parte != ParteCostruzione.FaldaLaterale)
                .Select(r => Math.Abs(r.Pressione)).DefaultIfEmpty(0).Max();
            double Spessore(double pressione) => massimo > 0 ? Math.Max(Math.Abs(pressione) / massimo * SpessoreMassimo, 3) : 3;

            // copertura: fasce normali al profilo, una per tratto di ogni zona
            var confini = new SortedSet<double> { 0, D };
            foreach (AzioneSuperficie riga in righe.Where(r => r.Parte == ParteCostruzione.Copertura))
            {
                foreach (TrattoSviluppo tratto in riga.Tratti)
                {
                    confini.Add(Math.Round(tratto.XInizio, 6)); confini.Add(Math.Round(tratto.XFine, 6));
                    FasciaCopertura(dc, profilo, P, tratto, riga, Spessore(riga.Pressione));
                }
            }

            // pareti: fasce orizzontali; la parete sopravento può avere due zone con quote di riferimento diverse
            double quotaInferiore = 0;
            foreach (AzioneSuperficie riga in righe.Where(r => r.Parte == ParteCostruzione.PareteSopravento).OrderBy(r => r.Ze))
            {
                double quotaSuperiore = Math.Min(riga.Ze, profilo[0].Y);
                if (ReferenceEquals(riga, righe.Where(r => r.Parte == ParteCostruzione.PareteSopravento).OrderBy(r => r.Ze).Last())) quotaSuperiore = profilo[0].Y;
                FasciaParete(dc, P(0, quotaInferiore), P(0, quotaSuperiore), -1, riga, Spessore(riga.Pressione));
                quotaInferiore = quotaSuperiore;
            }
            foreach (AzioneSuperficie riga in righe.Where(r => r.Parte == ParteCostruzione.PareteSottovento))
            {
                FasciaParete(dc, P(D, 0), P(D, profilo[^1].Y), 1, riga, Spessore(riga.Pressione));
            }

            // ascisse dei confini fra le zone, dal bordo sopravento
            double yQuota = origineY + 22;
            dc.DrawLine(Quota, new Point(x0, yQuota), new Point(x1, yQuota));
            double ultimaEtichetta = double.NegativeInfinity;
            foreach (double x in confini)
            {
                double px = P(x, 0).X;
                dc.DrawLine(Quota, new Point(px, yQuota - 5), new Point(px, yQuota + 5));
                if (px - ultimaEtichetta < 34) continue;
                Scrivi(dc, x.ToString("0.##", CultureInfo.CurrentCulture), new Point(px, yQuota + 6), TestoQuota, 10.5, false, 0.5, 0);
                ultimaEtichetta = px;
            }
            Scrivi(dc, "x [m]", new Point(x1 + 12, yQuota), TestoQuota, 10.5, false, 0, 0.5);

            // superfici fuori dalla sezione
            string fuori = string.Join("   ·   ", righe.Where(r => r.Parte is ParteCostruzione.PareteLaterale or ParteCostruzione.FaldaLaterale)
                .Select(r => r.Superficie + ": " + Valore(r.Pressione) + " N/m²"));
            if (fuori.Length > 0) Scrivi(dc, fuori, new Point(12, ActualHeight - 8), Testo, 10.5, false, 0, 1);

            double yVento = origineY - Math.Max(sommità * scalaY * 0.5, 26);
            FrecciaVento(dc, new Point(18, yVento), new Point(64, yVento));
            Scrivi(dc, "vento", new Point(41, yVento + 9), ColoreVento, 11, false, 0.5, 0);
        }

        private static string Valore(double pressione) => pressione.ToString("+#,0;-#,0;0", CultureInfo.CurrentCulture);

        /// <summary>Lettera della zona quando la Circolare la identifica (A, B, C); altrimenti nessuna</summary>
        private static string Zona(AzioneSuperficie riga)
        {
            string s = riga.Superficie;
            if (s.Contains("zona A") || s.Contains("fascia sopravento")) return "A";
            if (s.Contains("zone B") || s.Contains("restanti zone")) return "B";
            if (s.Contains("zona C")) return "C";
            return "";
        }

        private void Legenda(DrawingContext dc)
        {
            double x = ActualWidth - 12;
            Scrivi(dc, "pressione (p > 0)", new Point(x, 10), TestoPressione, 10.5, false, 1, 0);
            dc.DrawRectangle(Pressione, PennaPressione, new Rect(x - 112, 11, 12, 10));
            Scrivi(dc, "depressione (p < 0)", new Point(x, 26), TestoDepressione, 10.5, false, 1, 0);
            dc.DrawRectangle(Depressione, PennaDepressione, new Rect(x - 122, 27, 12, 10));
        }

        /// <summary>Quota del profilo all'ascissa x, per interpolazione lineare fra i vertici</summary>
        private static double QuotaProfilo(List<Point> profilo, double x)
        {
            for (int i = 1; i < profilo.Count; i++)
            {
                Point a = profilo[i - 1], b = profilo[i];
                if (b.X - a.X < 1e-9) continue;
                if (x <= b.X + 1e-9) return a.Y + (b.Y - a.Y) * Math.Clamp((x - a.X) / (b.X - a.X), 0, 1);
            }
            return profilo[^1].Y;
        }

        private void FasciaCopertura(DrawingContext dc, List<Point> profilo, Func<double, double, Point> P, TrattoSviluppo tratto, AzioneSuperficie riga, double spessore)
        {
            double inizio = Math.Min(tratto.XInizio, tratto.XFine), fine = Math.Max(tratto.XInizio, tratto.XFine);
            if (fine - inizio < 1e-9) return;

            // punti del profilo compresi nel tratto, estremi inclusi
            var ascisse = new SortedSet<double> { inizio, fine };
            foreach (Point p in profilo) if (p.X > inizio + 1e-9 && p.X < fine - 1e-9) ascisse.Add(p.X);
            List<Point> linea = ascisse.Select(x => P(x, QuotaProfilo(profilo, x))).ToList();

            bool positivo = riga.Pressione > 0;
            Brush riempimento = positivo ? Pressione : Depressione;
            Pen penna = positivo ? PennaPressione : PennaDepressione;

            // per ogni segmento un quadrilatero spostato lungo la normale esterna (verso l'alto)
            var esterno = new List<Point>();
            for (int i = 1; i < linea.Count; i++)
            {
                Vector direzione = linea[i] - linea[i - 1];
                if (direzione.Length < 1e-6) continue;
                direzione.Normalize();
                var normale = new Vector(direzione.Y, -direzione.X) * spessore;
                var quadrilatero = new StreamGeometry();
                using (StreamGeometryContext g = quadrilatero.Open())
                {
                    g.BeginFigure(linea[i - 1], true, true);
                    g.LineTo(linea[i - 1] + normale, false, false);
                    g.LineTo(linea[i] + normale, false, false);
                    g.LineTo(linea[i], false, false);
                }
                quadrilatero.Freeze();
                dc.DrawGeometry(riempimento, null, quadrilatero);
                dc.DrawLine(penna, linea[i - 1] + normale, linea[i] + normale);
                esterno.Add(linea[i - 1] + normale); esterno.Add(linea[i] + normale);
            }
            if (esterno.Count == 0) return;
            dc.DrawLine(penna, linea[0], esterno[0]);
            dc.DrawLine(penna, linea[^1], esterno[^1]);

            // frecce a 1/4, 1/2 e 3/4 del tratto; etichetta al centro, oltre la fascia
            double lunghezza = (linea[^1] - linea[0]).Length;
            int frecce = lunghezza > 90 ? 3 : 1;
            for (int k = 1; k <= frecce; k++)
            {
                double x = inizio + (fine - inizio) * k / (frecce + 1);
                Point piede = P(x, QuotaProfilo(profilo, x));
                Vector normale = Normale(profilo, P, x);
                Freccia(dc, penna, positivo ? piede + normale * spessore : piede, positivo ? piede : piede + normale * spessore);
            }
            double centro = (inizio + fine) / 2;
            Point ancora = P(centro, QuotaProfilo(profilo, centro)) + Normale(profilo, P, centro) * (spessore + 5);
            string zona = Zona(riga);
            string testo = (zona.Length > 0 ? zona + "   " : "") + Valore(riga.Pressione);
            if (riga.Forza.HasValue) testo += "   F = " + riga.Forza.Value.ToString("0.0", CultureInfo.CurrentCulture) + " kN";
            Scrivi(dc, testo, ancora, positivo ? TestoPressione : TestoDepressione, 11, true, 0.5, 1);
        }

        /// <summary>Normale esterna unitaria al profilo, in coordinate dello schermo</summary>
        private static Vector Normale(List<Point> profilo, Func<double, double, Point> P, double x)
        {
            double passo = Math.Max((profilo[^1].X - profilo[0].X) / 400, 1e-6);
            Vector direzione = P(x + passo, QuotaProfilo(profilo, x + passo)) - P(x - passo, QuotaProfilo(profilo, x - passo));
            if (direzione.Length < 1e-9) return new Vector(0, -1);
            direzione.Normalize();
            return new Vector(direzione.Y, -direzione.X);
        }

        /// <summary>Fascia su una parete verticale: lato = −1 a sinistra (sopravento), +1 a destra (sottovento)</summary>
        private void FasciaParete(DrawingContext dc, Point basso, Point alto, int lato, AzioneSuperficie riga, double spessore)
        {
            if (basso.Y - alto.Y < 2) return;
            bool positivo = riga.Pressione > 0;
            Pen penna = positivo ? PennaPressione : PennaDepressione;
            double xEsterno = basso.X + lato * spessore;
            dc.DrawRectangle(positivo ? Pressione : Depressione, penna, new Rect(new Point(basso.X, alto.Y), new Point(xEsterno, basso.Y)));

            int frecce = basso.Y - alto.Y > 70 ? 3 : 1;
            for (int k = 1; k <= frecce; k++)
            {
                double y = basso.Y - (basso.Y - alto.Y) * k / (frecce + 1);
                var sullaParete = new Point(basso.X, y); var fuori = new Point(xEsterno, y);
                Freccia(dc, penna, positivo ? fuori : sullaParete, positivo ? sullaParete : fuori);
            }
            Scrivi(dc, Valore(riga.Pressione), new Point(xEsterno + lato * 6, (basso.Y + alto.Y) / 2), positivo ? TestoPressione : TestoDepressione, 11, true, lato < 0 ? 1 : 0, 0.5);
        }

        private static void Freccia(DrawingContext dc, Pen penna, Point da, Point a)
        {
            Vector direzione = a - da;
            if (direzione.Length < 6) return;
            direzione.Normalize();
            var trasversale = new Vector(-direzione.Y, direzione.X);
            dc.DrawLine(penna, da, a);
            dc.DrawLine(penna, a, a - direzione * 6 + trasversale * 3);
            dc.DrawLine(penna, a, a - direzione * 6 - trasversale * 3);
        }
    }
}
