using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.Specialized;

namespace STA.Geometria
{
    /// <summary>
    /// Classe per la gestione dei punti
    /// </summary>
    public class Punto2D
    {
        private string _ID { get; set; }
        private double _X { get; set; }
        private double _Y { get; set; }

        /// <summary>
        /// Coordinata X in cm
        /// </summary>
        public double X
        {
            get { return _X; }
            set
            {
                _X = value;
            }
        }

        /// <summary>
        /// Coordinata Y in cm
        /// </summary>
        public double Y
        {
            get { return _Y; }
            set
            {
                _Y = value;
            }
        }

        /// <summary>
        /// Identificativo
        /// </summary>
        public string ID
        {
            get { return _ID; }
            set { _ID = value; }
        }
   }
}
