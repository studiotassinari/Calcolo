using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Vento.Aerodinamica;

namespace STA.Calcolo.Desktop.Controls
{
    /// <summary>
    /// Disegna lo schema quotato della costruzione (sezione e pianta) con gli stessi simboli
    /// delle caselle di immissione: b, d, h, α, f. Il disegno segue i valori inseriti.
    /// È un elemento di sola presentazione: non contiene logica di calcolo.
    /// </summary>
    public class WindBuildingDrawing : FrameworkElement
    {
        #region proprietà di dipendenza

        public static readonly DependencyProperty TypeProperty = Registra(nameof(Type), typeof(TipologiaCostruzione), TipologiaCostruzione.CoperturaFaldaDoppia);
        public static readonly DependencyProperty DirectionProperty = Registra(nameof(Direction), typeof(DirezioneVento), DirezioneVento.PerpendicolareAlColmo);
        public static readonly DependencyProperty MultiSchemeProperty = Registra(nameof(MultiScheme), typeof(SchemaFaldeMultiple), SchemaFaldeMultiple.FaldeDoppieConColmi);
        public static readonly DependencyProperty BProperty = Registra(nameof(B), typeof(double), 20.0);
        public static readonly DependencyProperty DProperty = Registra(nameof(D), typeof(double), 12.0);
        public static readonly DependencyProperty HProperty = Registra(nameof(H), typeof(double), 8.0);
        public static readonly DependencyProperty AlphaProperty = Registra(nameof(Alpha), typeof(double), 15.0);
        public static readonly DependencyProperty RiseProperty = Registra(nameof(Rise), typeof(double), 2.0);
        public static readonly DependencyProperty SpansProperty = Registra(nameof(Spans), typeof(int), 3);

        private static DependencyProperty Registra(string nome, Type tipo, object predefinito)
        {
            return DependencyProperty.Register(nome, tipo, typeof(WindBuildingDrawing),
                new FrameworkPropertyMetadata(predefinito, FrameworkPropertyMetadataOptions.AffectsRender));
        }

        public TipologiaCostruzione Type
        {
            get { return (TipologiaCostruzione)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public DirezioneVento Direction
        {
            get { return (DirezioneVento)GetValue(DirectionProperty); }
            set { SetValue(DirectionProperty, value); }
        }

        public SchemaFaldeMultiple MultiScheme
        {
            get { return (SchemaFaldeMultiple)GetValue(MultiSchemeProperty); }
            set { SetValue(MultiSchemeProperty, value); }
        }

        public double B
        {
            get { return (double)GetValue(BProperty); }
            set { SetValue(BProperty, value); }
        }

        public double D
        {
            get { return (double)GetValue(DProperty); }
            set { SetValue(DProperty, value); }
        }

        public double H
        {
            get { return (double)GetValue(HProperty); }
            set { SetValue(HProperty, value); }
        }

        public double Alpha
        {
            get { return (double)GetValue(AlphaProperty); }
            set { SetValue(AlphaProperty, value); }
        }

        public double Rise
        {
            get { return (double)GetValue(RiseProperty); }
            set { SetValue(RiseProperty, value); }
        }

        public int Spans
        {
            get { return (int)GetValue(SpansProperty); }
            set { SetValue(SpansProperty, value); }
        }

        #endregion

        #region risorse grafiche

        // Colori dell'identità Tassinari Associati, gli stessi del tema Themes/Tassinari.xaml e degli altri disegni dell'app.
        protected static readonly Color Navy = Color.FromRgb(0x19, 0x28, 0x45);
        protected static readonly Color Petrolio = Color.FromRgb(0x13, 0x5F, 0x6E);
        protected static readonly Color Ocra = Color.FromRgb(0xC3, 0x87, 0x2A);

        protected static readonly Brush Sfondo = Brushes.White;
        protected static readonly Brush Riempimento = Congela(new SolidColorBrush(Color.FromRgb(0xEA, 0xF0, 0xF1)));
        protected static readonly Brush RiempimentoFascia = Congela(new SolidColorBrush(Color.FromArgb(0x66, Ocra.R, Ocra.G, Ocra.B)));
        protected static readonly Brush Testo = Congela(new SolidColorBrush(Color.FromRgb(0x52, 0x67, 0x78)));
        protected static readonly Brush TestoTitolo = Congela(new SolidColorBrush(Navy));
        protected static readonly Brush TestoQuota = Congela(new SolidColorBrush(Ocra));
        protected static readonly Brush ColoreVento = Congela(new SolidColorBrush(Petrolio));
        protected static readonly Pen Contorno = Congela(new Pen(new SolidColorBrush(Navy), 2));
        protected static readonly Pen Sottile = Congela(new Pen(new SolidColorBrush(Navy), 0.8));
        protected static readonly Pen Terreno = Congela(new Pen(new SolidColorBrush(Color.FromRgb(0x52, 0x67, 0x78)), 2.5));
        protected static readonly Pen TerrenoSotto = Congela(new Pen(new SolidColorBrush(Color.FromRgb(0xB8, 0xCD, 0xD1)), 2));
        protected static readonly Pen Bordo = Congela(new Pen(new SolidColorBrush(Color.FromRgb(0xD6, 0xE0, 0xE2)), 1));
        protected static readonly Pen Quota = Congela(new Pen(TestoQuota, 1.2));
        protected static readonly Pen Tratteggio = Congela(new Pen(new SolidColorBrush(Navy), 1) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) });
        protected static readonly Pen TratteggioQuota = Congela(new Pen(TestoQuota, 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) });
        protected static readonly Pen PennaVento = Congela(new Pen(ColoreVento, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        // Carattere aziendale incorporato nell'app.
        protected static readonly FontFamily Acherus = new FontFamily(new Uri("pack://application:,,,/STA.Calcolo.Desktop;component/Assets/Brand/"), "./#Acherus Grotesque");
        protected static readonly Typeface Carattere = new Typeface(Acherus, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        protected static readonly Typeface CarattereCorsivo = new Typeface(Acherus, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        protected static T Congela<T>(T risorsa) where T : Freezable
        {
            risorsa.Freeze();
            return risorsa;
        }

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            double larghezza = double.IsInfinity(availableSize.Width) ? 620 : availableSize.Width;
            double altezza = double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height;
            return new Size(larghezza, altezza);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var area = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawRoundedRectangle(Sfondo, Bordo, new Rect(0.5, 0.5, Math.Max(ActualWidth - 1, 0), Math.Max(ActualHeight - 1, 0)), 4, 4);

            if (ActualWidth < 240 || ActualHeight < 140) return;

            if (!(B > 0) || !(D > 0) || !(H > 0))
            {
                Scrivi(dc, "Inserire b, d e h per visualizzare lo schema", new Point(area.Width / 2, area.Height / 2), Testo, 12, false, 0.5, 0.5);
                return;
            }

            double metà = Math.Floor(ActualWidth * 0.56);
            dc.DrawLine(Bordo, new Point(metà, 8), new Point(metà, ActualHeight - 8));

            DisegnaSezione(dc, new Rect(0, 0, metà, ActualHeight));
            DisegnaPianta(dc, new Rect(metà, 0, ActualWidth - metà, ActualHeight));
        }

        #region geometria del profilo

        protected bool ÈTettoia =>
            Type == TipologiaCostruzione.TettoiaFaldaSingola ||
            Type == TipologiaCostruzione.TettoiaFaldaDoppia ||
            Type == TipologiaCostruzione.TettoiaFaldeMultiple;

        protected bool ÈVolta => Type == TipologiaCostruzione.CoperturaVoltaCilindrica;

        /// <summary>La direzione del vento è rilevante solo per le tipologie con colmo o generatrici</summary>
        protected bool VentoParallelo =>
            Direction == DirezioneVento.ParalleloAlColmo &&
            Type != TipologiaCostruzione.CoperturaPiana &&
            Type != TipologiaCostruzione.CoperturaPadiglione;

        /// <summary>
        /// Profilo della copertura in coordinate reali (x lungo la sezione, y quota), da sinistra a destra
        /// </summary>
        protected List<Point> ProfiloCopertura(double larghezza, out double quotaMassima)
        {
            var punti = new List<Point>();
            double tangente = Math.Tan(Math.Min(Math.Abs(Alpha), 80) * Math.PI / 180);
            bool positivo = Alpha >= 0;
            int tratti = Math.Clamp(Spans, 1, 12);
            quotaMassima = H;

            switch (Type)
            {
                case TipologiaCostruzione.CoperturaPiana:
                    punti.Add(new Point(0, H));
                    punti.Add(new Point(larghezza, H));
                    break;

                case TipologiaCostruzione.CoperturaFaldaSingola:
                case TipologiaCostruzione.TettoiaFaldaSingola:
                    {
                        double salita = Math.Min(larghezza * tangente, 0.85 * H);
                        // nella sezione trasversale (vento parallelo) il segno di α non ha significato
                        bool bassoASinistra = positivo || VentoParallelo;
                        punti.Add(new Point(0, bassoASinistra ? H - salita : H));
                        punti.Add(new Point(larghezza, bassoASinistra ? H : H - salita));
                    }
                    break;

                case TipologiaCostruzione.CoperturaFaldaDoppia:
                case TipologiaCostruzione.CoperturaPadiglione:
                case TipologiaCostruzione.TettoiaFaldaDoppia:
                    {
                        double salita = Math.Min(larghezza / 2 * tangente, 0.85 * H);
                        bool colmo = positivo || Type == TipologiaCostruzione.CoperturaPadiglione;
                        punti.Add(new Point(0, colmo ? H - salita : H));
                        punti.Add(new Point(larghezza / 2, colmo ? H : H - salita));
                        punti.Add(new Point(larghezza, colmo ? H - salita : H));
                    }
                    break;

                case TipologiaCostruzione.CoperturaFaldeMultiple:
                case TipologiaCostruzione.TettoiaFaldeMultiple:
                    {
                        double passo = larghezza / tratti;
                        SchemaFaldeMultiple schema = Type == TipologiaCostruzione.TettoiaFaldeMultiple
                            ? (positivo ? SchemaFaldeMultiple.FaldeDoppieConColmi : SchemaFaldeMultiple.FaldeDoppieConImpluvi)
                            : MultiScheme;
                        bool shed = schema == SchemaFaldeMultiple.ShedInclinazioneNegativa || schema == SchemaFaldeMultiple.ShedInclinazionePositiva;
                        double salita = Math.Min((shed ? passo : passo / 2) * tangente, 0.6 * H);

                        for (int i = 0; i < tratti; i++)
                        {
                            double x = i * passo;
                            switch (schema)
                            {
                                case SchemaFaldeMultiple.ShedInclinazioneNegativa:
                                    punti.Add(new Point(x, H));
                                    punti.Add(new Point(x + passo, H - salita));
                                    break;
                                case SchemaFaldeMultiple.ShedInclinazionePositiva:
                                    punti.Add(new Point(x, H - salita));
                                    punti.Add(new Point(x + passo, H));
                                    break;
                                case SchemaFaldeMultiple.FaldeDoppieConColmi:
                                    if (i == 0) punti.Add(new Point(x, H - salita));
                                    punti.Add(new Point(x + passo / 2, H));
                                    punti.Add(new Point(x + passo, H - salita));
                                    break;
                                default:
                                    if (i == 0) punti.Add(new Point(x, H));
                                    punti.Add(new Point(x + passo / 2, H - salita));
                                    punti.Add(new Point(x + passo, H));
                                    break;
                            }
                        }
                    }
                    break;

                case TipologiaCostruzione.CoperturaVoltaCilindrica:
                    if (VentoParallelo)
                    {
                        // sezione lungo le generatrici: la volta appare come una linea orizzontale in chiave
                        double chiave = H + Math.Max(Rise, 0);
                        punti.Add(new Point(0, chiave));
                        punti.Add(new Point(larghezza, chiave));
                        quotaMassima = chiave;
                    }
                    else
                    {
                        double f = Math.Clamp(Rise, 0.001, larghezza / 2);
                        double raggio = (larghezza * larghezza / 4 + f * f) / (2 * f);
                        double centroY = H + f - raggio;
                        double semiAngolo = Math.Asin(Math.Min(larghezza / 2 / raggio, 1));
                        const int passi = 28;
                        for (int i = 0; i <= passi; i++)
                        {
                            double angolo = -semiAngolo + 2 * semiAngolo * i / passi;
                            punti.Add(new Point(larghezza / 2 + raggio * Math.Sin(angolo), centroY + raggio * Math.Cos(angolo)));
                        }
                        quotaMassima = H + f;
                    }
                    break;
            }

            return punti;
        }

        #endregion

        #region sezione

        private void DisegnaSezione(DrawingContext dc, Rect r)
        {
            bool parallelo = VentoParallelo;

            // con vento parallelo al colmo si mostra la sezione trasversale, che si sviluppa lungo b;
            // per la volta si mostra invece la sezione lungo le generatrici (lungo d)
            bool sezioneLungoB = parallelo && !ÈVolta;
            double larghezza = sezioneLungoB ? B : D;
            string simboloLarghezza = sezioneLungoB ? "b" : "d";

            List<Point> profilo = ProfiloCopertura(larghezza, out double quotaMassima);

            const double margineSinistro = 78, margineDestro = 62, margineAlto = 34, margineBasso = 44;
            double disponibileX = r.Width - margineSinistro - margineDestro;
            double disponibileY = r.Height - margineAlto - margineBasso;

            // proporzioni reali, limitate per mantenere leggibile il disegno
            double rapporto = Math.Clamp(quotaMassima / larghezza, 0.22, 1.5);
            double scalaX = Math.Min(disponibileX / larghezza, disponibileY / (rapporto * larghezza));
            double scalaY = scalaX * rapporto * larghezza / quotaMassima;
            double origineX = r.X + margineSinistro + (disponibileX - larghezza * scalaX) / 2;
            double origineY = r.Bottom - margineBasso;

            Point P(double x, double y) => new Point(origineX + x * scalaX, origineY - y * scalaY);

            string titolo = sezioneLungoB
                ? "Sezione trasversale (vento ortogonale al foglio)"
                : "Sezione nella direzione del vento";
            Scrivi(dc, titolo, new Point(r.X + 12, r.Y + 9), TestoTitolo, 12, false, 0, 0);

            // terreno
            double x0 = P(0, 0).X, x1 = P(larghezza, 0).X;
            dc.DrawLine(Terreno, new Point(x0 - 30, origineY + 2.5), new Point(x1 + 30, origineY + 2.5));
            dc.DrawLine(TerrenoSotto, new Point(x0 - 24, origineY + 7), new Point(x1 + 24, origineY + 7));

            // costruzione
            if (ÈTettoia)
            {
                dc.DrawLine(Tratteggio, P(0, 0), P(0, profilo[0].Y));
                dc.DrawLine(Tratteggio, P(larghezza, 0), P(larghezza, profilo[profilo.Count - 1].Y));
                for (int i = 1; i < profilo.Count; i++)
                {
                    dc.DrawLine(Contorno, P(profilo[i - 1].X, profilo[i - 1].Y), P(profilo[i].X, profilo[i].Y));
                }
            }
            else
            {
                var figura = new StreamGeometry();
                using (StreamGeometryContext g = figura.Open())
                {
                    g.BeginFigure(P(0, 0), true, true);
                    foreach (Point p in profilo) g.LineTo(P(p.X, p.Y), true, false);
                    g.LineTo(P(larghezza, 0), true, false);
                }
                figura.Freeze();
                dc.DrawGeometry(Riempimento, Contorno, figura);

                if (ÈVolta && !parallelo)
                {
                    dc.DrawLine(Tratteggio, P(0, H), P(larghezza, H));
                }
            }

            // quota orizzontale
            QuotaOrizzontale(dc, x0, x1, origineY + 24, simboloLarghezza);

            // quote verticali
            double xQuota = x1 + 26;
            if (ÈVolta)
            {
                QuotaVerticale(dc, xQuota, origineY, P(0, H).Y, "h");
                QuotaVerticale(dc, xQuota, P(0, H).Y, P(0, quotaMassima).Y, "f");

                double ze = H + Math.Max(Rise, 0) / 2;
                double yZe = P(0, ze).Y;
                dc.DrawLine(TratteggioQuota, new Point(x0 - 8, yZe), new Point(x1 + 8, yZe));
                Scrivi(dc, "ze = h + f/2", new Point(x0 - 10, yZe - 2), TestoQuota, 10.5, false, 1, 1);
            }
            else
            {
                QuotaVerticale(dc, xQuota, origineY, P(0, quotaMassima).Y, "h");
            }

            // angolo α sulla prima falda inclinata
            DisegnaAngolo(dc, profilo, P);

            // etichette alto / basso per la falda singola
            if ((Type == TipologiaCostruzione.CoperturaFaldaSingola || Type == TipologiaCostruzione.TettoiaFaldaSingola) && profilo.Count == 2
                && Math.Abs(profilo[0].Y - profilo[1].Y) > 1e-9)
            {
                bool sinistraBassa = profilo[0].Y < profilo[1].Y;
                Scrivi(dc, sinistraBassa ? "basso" : "alto", P(0, profilo[0].Y) + new Vector(2, -4), Testo, 10, false, 0, 1);
                Scrivi(dc, sinistraBassa ? "alto" : "basso", P(larghezza, profilo[1].Y) + new Vector(-2, -4), Testo, 10, false, 1, 1);
            }

            // vento
            double yVento = origineY - Math.Max(quotaMassima * scalaY * 0.55, 30);
            if (sezioneLungoB)
            {
                var centro = new Point(x0 - 44, yVento);
                dc.DrawEllipse(null, PennaVento, centro, 11, 11);
                dc.DrawLine(PennaVento, centro + new Vector(-7, -7), centro + new Vector(7, 7));
                dc.DrawLine(PennaVento, centro + new Vector(-7, 7), centro + new Vector(7, -7));
                Scrivi(dc, "vento", centro + new Vector(0, 14), ColoreVento, 11, false, 0.5, 0);
            }
            else
            {
                FrecciaVento(dc, new Point(x0 - 68, yVento), new Point(x0 - 16, yVento));
                Scrivi(dc, "vento", new Point(x0 - 42, yVento + 9), ColoreVento, 11, false, 0.5, 0);
            }
        }

        private void DisegnaAngolo(DrawingContext dc, List<Point> profilo, Func<double, double, Point> P)
        {
            if (ÈVolta || Type == TipologiaCostruzione.CoperturaPiana) return;

            for (int i = 1; i < profilo.Count; i++)
            {
                Point a = profilo[i - 1], b = profilo[i];
                if (Math.Abs(a.Y - b.Y) < 1e-9 || Math.Abs(a.X - b.X) < 1e-9) continue;

                Point basso = a.Y < b.Y ? a : b;
                Point alto = a.Y < b.Y ? b : a;
                Point pb = P(basso.X, basso.Y), pa = P(alto.X, alto.Y);
                double verso = Math.Sign(pa.X - pb.X);
                double lunghezza = Math.Min(Math.Abs(pa.X - pb.X) * 0.8, 46);
                if (lunghezza < 12) return;

                // riferimento orizzontale e arco fra orizzontale e falda
                dc.DrawLine(TratteggioQuota, pb, new Point(pb.X + verso * lunghezza, pb.Y));
                double angolo = Math.Atan2(pb.Y - pa.Y, Math.Abs(pa.X - pb.X));
                double raggio = lunghezza * 0.72;
                var inizio = new Point(pb.X + verso * raggio, pb.Y);
                var fine = new Point(pb.X + verso * raggio * Math.Cos(angolo), pb.Y - raggio * Math.Sin(angolo));
                var arco = new StreamGeometry();
                using (StreamGeometryContext g = arco.Open())
                {
                    g.BeginFigure(inizio, false, false);
                    g.ArcTo(fine, new Size(raggio, raggio), 0, false,
                        verso > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise, true, false);
                }
                arco.Freeze();
                dc.DrawGeometry(null, Quota, arco);
                if (lunghezza >= 30)
                    Scrivi(dc, "α", new Point(pb.X + verso * (lunghezza + 9), pb.Y - 2), TestoQuota, 13, true, 0.5, 0.5);
                else
                    Scrivi(dc, "α", new Point(pb.X + verso * lunghezza / 2, pb.Y + 1), TestoQuota, 13, true, 0.5, 0);
                return;
            }
        }

        #endregion

        #region pianta

        private void DisegnaPianta(DrawingContext dc, Rect r)
        {
            bool parallelo = VentoParallelo;

            const double margineSinistro = 64, margineDestro = 46, margineAlto = 34, margineBasso = 44;
            double disponibileX = r.Width - margineSinistro - margineDestro;
            double disponibileY = r.Height - margineAlto - margineBasso;
            if (disponibileX < 40 || disponibileY < 40) return;

            double rapporto = Math.Clamp(B / D, 0.3, 3);
            double scalaX = Math.Min(disponibileX / D, disponibileY / (rapporto * D));
            double scalaY = scalaX * rapporto * D / B;
            double larghezza = D * scalaX, altezza = B * scalaY;
            double x0 = r.X + margineSinistro + (disponibileX - larghezza) / 2;
            double y0 = r.Y + margineAlto + (disponibileY - altezza) / 2;
            var pianta = new Rect(x0, y0, larghezza, altezza);

            Scrivi(dc, "Pianta", new Point(r.X + 12, r.Y + 9), TestoTitolo, 12, false, 0, 0);
            dc.DrawRectangle(ÈTettoia ? null : Riempimento, Contorno, pianta);

            // fascia sopravento di profondità min(b/2; h), dove la norma distingue le zone A e B
            bool fascia = !ÈTettoia &&
                          (Type == TipologiaCostruzione.CoperturaPiana ||
                           (parallelo && Type != TipologiaCostruzione.CoperturaPadiglione));
            if (fascia)
            {
                double sommità = ÈVolta ? H + Math.Max(Rise, 0) : H;
                double profondità = Math.Min(Math.Min(B / 2, sommità), D) * scalaX;
                dc.DrawRectangle(RiempimentoFascia, Sottile, new Rect(x0, y0, profondità, altezza));
                Scrivi(dc, "A", new Point(x0 + profondità / 2, y0 + altezza / 2), Testo, 12, true, 0.5, 0.5);
                if (larghezza - profondità > 16)
                {
                    Scrivi(dc, "B", new Point(x0 + profondità + (larghezza - profondità) / 2, y0 + altezza / 2), Testo, 12, true, 0.5, 0.5);
                }
                Scrivi(dc, "min(b/2; h)", new Point(x0, y0 - 3), TestoQuota, 10, false, 0, 1);
            }

            DisegnaColmi(dc, pianta, parallelo);

            QuotaOrizzontale(dc, x0, x0 + larghezza, y0 + altezza + 22, "d");
            QuotaVerticale(dc, x0 + larghezza + 24, y0 + altezza, y0, "b");

            double yVento = y0 + altezza / 2;
            FrecciaVento(dc, new Point(x0 - 56, yVento), new Point(x0 - 12, yVento));
            Scrivi(dc, "vento", new Point(x0 - 34, yVento + 9), ColoreVento, 11, false, 0.5, 0);
        }

        /// <summary>
        /// Linee di colmo, impluvio o generatrici in pianta
        /// </summary>
        private void DisegnaColmi(DrawingContext dc, Rect p, bool parallelo)
        {
            int tratti = Math.Clamp(Spans, 1, 12);

            void Linea(double frazione)
            {
                if (parallelo)
                    dc.DrawLine(Tratteggio, new Point(p.Left, p.Top + p.Height * frazione), new Point(p.Right, p.Top + p.Height * frazione));
                else
                    dc.DrawLine(Tratteggio, new Point(p.Left + p.Width * frazione, p.Top), new Point(p.Left + p.Width * frazione, p.Bottom));
            }

            switch (Type)
            {
                case TipologiaCostruzione.CoperturaFaldaDoppia:
                case TipologiaCostruzione.TettoiaFaldaDoppia:
                case TipologiaCostruzione.CoperturaVoltaCilindrica:
                    Linea(0.5);
                    break;

                case TipologiaCostruzione.CoperturaFaldeMultiple:
                case TipologiaCostruzione.TettoiaFaldeMultiple:
                    bool shed = Type == TipologiaCostruzione.CoperturaFaldeMultiple &&
                                (MultiScheme == SchemaFaldeMultiple.ShedInclinazioneNegativa || MultiScheme == SchemaFaldeMultiple.ShedInclinazionePositiva);
                    int linee = shed ? tratti : tratti * 2;
                    for (int i = 1; i < linee; i++) Linea((double)i / linee);
                    break;

                case TipologiaCostruzione.CoperturaPadiglione:
                    // colmo parallelo al lato maggiore, displuvi a 45° dagli angoli
                    if (p.Height >= p.Width)
                    {
                        double m = p.Width / 2;
                        var a = new Point(p.Left + m, p.Top + m);
                        var b = new Point(p.Left + m, p.Bottom - m);
                        dc.DrawLine(Tratteggio, a, b);
                        dc.DrawLine(Tratteggio, p.TopLeft, a);
                        dc.DrawLine(Tratteggio, p.TopRight, a);
                        dc.DrawLine(Tratteggio, p.BottomLeft, b);
                        dc.DrawLine(Tratteggio, p.BottomRight, b);
                    }
                    else
                    {
                        double m = p.Height / 2;
                        var a = new Point(p.Left + m, p.Top + m);
                        var b = new Point(p.Right - m, p.Top + m);
                        dc.DrawLine(Tratteggio, a, b);
                        dc.DrawLine(Tratteggio, p.TopLeft, a);
                        dc.DrawLine(Tratteggio, p.BottomLeft, a);
                        dc.DrawLine(Tratteggio, p.TopRight, b);
                        dc.DrawLine(Tratteggio, p.BottomRight, b);
                    }
                    break;
            }
        }

        #endregion

        #region primitive

        protected void QuotaOrizzontale(DrawingContext dc, double x1, double x2, double y, string simbolo)
        {
            dc.DrawLine(Quota, new Point(x1, y), new Point(x2, y));
            dc.DrawLine(Quota, new Point(x1, y - 5), new Point(x1, y + 5));
            dc.DrawLine(Quota, new Point(x2, y - 5), new Point(x2, y + 5));
            Scrivi(dc, simbolo, new Point((x1 + x2) / 2, y + 2), TestoQuota, 13, true, 0.5, 0);
        }

        protected void QuotaVerticale(DrawingContext dc, double x, double yBasso, double yAlto, string simbolo)
        {
            dc.DrawLine(Quota, new Point(x, yBasso), new Point(x, yAlto));
            dc.DrawLine(Quota, new Point(x - 5, yBasso), new Point(x + 5, yBasso));
            dc.DrawLine(Quota, new Point(x - 5, yAlto), new Point(x + 5, yAlto));
            Scrivi(dc, simbolo, new Point(x + 6, (yBasso + yAlto) / 2), TestoQuota, 13, true, 0, 0.5);
        }

        protected static void FrecciaVento(DrawingContext dc, Point da, Point a)
        {
            dc.DrawLine(PennaVento, da, new Point(a.X - 6, a.Y));
            var punta = new StreamGeometry();
            using (StreamGeometryContext g = punta.Open())
            {
                g.BeginFigure(a, true, true);
                g.LineTo(new Point(a.X - 12, a.Y - 6), true, false);
                g.LineTo(new Point(a.X - 12, a.Y + 6), true, false);
            }
            punta.Freeze();
            dc.DrawGeometry(ColoreVento, null, punta);
        }

        /// <summary>
        /// Scrive un testo; ancoraX e ancoraY (0 ÷ 1) indicano quale punto del testo coincide con la posizione
        /// </summary>
        protected void Scrivi(DrawingContext dc, string testo, Point posizione, Brush colore, double dimensione, bool corsivo, double ancoraX, double ancoraY)
        {
            var formattato = new FormattedText(testo, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                corsivo ? CarattereCorsivo : Carattere, dimensione, colore, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formattato, new Point(posizione.X - formattato.Width * ancoraX, posizione.Y - formattato.Height * ancoraY));
        }

        #endregion
    }
}
