using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using Geometria.Attributes;
using System.Xaml;
using System.Windows.Shapes;
using System.Windows;
using System.Xml.Serialization;
using STA.Geometria;
using System.Windows.Media;
using System.ComponentModel;

namespace STA.Geometria.Masse
{
    /// <summary>
    /// Classe per la gestione della sezione
    /// </summary>
    //[InputProperty("Sezione")] //Diamogli un'attributo
    [XmlRoot()]                 //e voglio anche provare la serializzazione con xml
    public class Sezione
    {
        #region proprietàPrivate

        /// <summary>
        /// indica il nome della Sezione
        /// Serve per identificarla
        /// </summary>
        private string _Nome { get; set; }

        #endregion

        #region prop. Pubbliche

        /// <summary>
        /// Identificativo della sezione
        /// </summary>
        public string Nome
        {
            get { return _Nome; }
            set { _Nome = value; }
        }


        /// <summary>
        /// lista dei punti
        /// </summary>
        public List<Punto2D> Vertici { get; set; }

        /// <summary>
        /// Area
        /// </summary>
        public double Area
        {
            get;
            set;
        }

        public Punto2D Baricentro
        {
            get;
            set;
        }

        //public PointCollection puntiGraficiFigura
        //{
        //    get
        //    {
        //        List<Point> puntiProv = new List<Point>();
        //        foreach (puntoFigura punto in puntiFigura)
        //        {
        //            puntiProv.Add(new Point(punto.X, punto.Y));
        //        }
        //        return new PointCollection(puntiProv);
        //    }
        //}

        //public PuntiFigura Punti = new PuntiFigura();

        #endregion



        #region Costruttori

        public Sezione()
        {
            Vertici = new List<Punto2D>();
            Baricentro = new Punto2D();
        }

        #endregion

        #region Metodi

        public void RicalcolaProprietà()
        {
            if (Vertici.Count >= 3)
            {
                //Area = Functions.Functions.FortranArea2D(this.Vertici);
                Area = Functions.Functions.Area2D(this.Vertici);
                Baricentro = Functions.Functions.calcolaBaricentro(this.Vertici);
            }
            
        }

        #endregion
    }

}
