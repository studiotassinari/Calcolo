﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using STA_Prefabbricato.data;
using System.Xml;

namespace STA_Dimensionamento_Plinti.pali
{
    /// <summary>
    /// Iterfaccia per la gestione dei pali di varia natura
    /// </summary>
    public interface IPalo
    {
        /// <summary>
        /// Portata limite del Palo
        /// </summary>
        double PortataLimite { get; }
        double Diametro { get; }
        
        punto2D Posizione { get; }

        XmlNode salvaXML();

        

        void AssegnaPosizione(punto2D puntoPosizione);

        double Area();

    }
}
