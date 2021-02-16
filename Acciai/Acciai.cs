using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace Acciai
{

    /// <summary>
    /// Elenco dei tipi di acciaio presenti in normativa
    /// </summary>
    public enum TipoAcciai
    {
        /// <summary>
        /// Acciaio tipo B450C
        /// </summary>
        B450C,

        /// <summary>
        /// Acciao tipo B450A
        /// </summary>
        B450A
   
    }

    

    public class AcciaioArmatura
    {
        
        #region Proprietà interne



        /// <summary>
        /// Tipo di acciaio associato
        /// </summary>
        private TipoAcciai _Tipo { get; set; }



        
        #endregion

        #region Prop. esterne

        /// <summary>
        /// Tipo di acciaio associato
        /// </summary>
        public TipoAcciai Tipo { get { return _Tipo; } }

        /// <summary>
        /// Caratteristica a snervamento [N/mmq]
        /// </summary>
        public double fy { get { return 450; } }

        /// <summary>
        /// Tensione caratteristica a rottura [N/mmq]
        /// </summary>
        public double ft { get { return 540; } }


        /// <summary>
        /// Tensione di progetto a snervamento [N/mmq]
        /// </summary>
        public double fyd { get { return (fy / 1.15); } }

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore con ingresso del tipo
        /// </summary>
        /// <param name="tipo">tipo di acciaio</param>
        public AcciaioArmatura(TipoAcciai tipo)
        {
            _Tipo = tipo;
            switch (Tipo)
            {
                case TipoAcciai.B450A:
                    
                    break;

                case TipoAcciai.B450C:
                    
                    break;
            }
        }

        /// <summary>
        /// Costruttore Base
        /// </summary>
        public AcciaioArmatura() : this(TipoAcciai.B450C)
        {
        }

        #endregion


    }

    public class ClassiAcciaioArmaturaToString : IValueConverter
    {

        #region IValueConverter Membri di

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (TipoAcciai)Enum.Parse(typeof(TipoAcciai), value.ToString(), true);
        }

        #endregion
    }
}
