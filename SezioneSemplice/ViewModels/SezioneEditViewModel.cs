using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.ComponentModel;
using System.Collections.ObjectModel;


using STA.Common;
using STA.Geometria.Masse.Commands;

using STA.Geometria.ViewModels;

namespace STA.Geometria.Masse.ViewModels
{
    public class SezioneEditViewModel : ViewModelBase
    {
        private Sezione _sezione;

        #region Membri

        /// <summary>
        /// classe modello associato al viewmodel
        /// </summary>
        public Sezione sezione
        {
            get { return _sezione; }
            set 
            { 
                _sezione = value;
                RaisePropertyChangedEvent("sezione");
            }
        }

        #endregion

        #region Membri pubblici

        /// <summary>
        /// Nome della sezione
        /// </summary>
        public string NomeSezione
        {
            get { return sezione.Nome; }
            set
            {
                sezione.Nome = value;
                RaisePropertyChangedEvent("NomeSezione");
            }
        }

        public ICommand AggiungiVerticeComando { get; set; }

        public ObservableCollection<Punto2DViewModel> Vertici { get; set; }

        public double Area
        {
            get {return sezione.Area;}
         }


        public Punto2DViewModel BaricentroVM
        {
            get { return new Punto2DViewModel(sezione.Baricentro); }
        }

        #endregion

        #region Costruttori

        /// <summary>
        /// Costruttore con parametro
        /// </summary>
        /// <param name="sezIn">Sezione in ingresso</param>
        public SezioneEditViewModel(Sezione sezIn)
        {
            _sezione = sezIn;
            AggiungiVerticeComando = new AggiungiVerticeCommand(this);
        }

        /// <summary>
        /// Costruttore senza parametro
        /// </summary>
        public SezioneEditViewModel() : this(new Sezione())
        {
            
        }

        #endregion


        #region Metodi

        /// <summary>
        /// Aggiunge un vertice in coda alla lista
        /// </summary>
        /// <param name="puntoIn"></param>
        public void AggiungiVertice()
        {
            Punto2D puntoNuovo = new Punto2D { ID = (sezione.Vertici.Count + 1).ToString() };
            sezione.Vertici.Add(puntoNuovo);
            AggiornaProprietà();
        }

        /// <summary>
        /// Aggiorna tutte le proprietà
        /// </summary>
        public void AggiornaProprietà()
        {
            sezione.RicalcolaProprietà();
            if (Vertici == null)
            {
                Vertici = new ObservableCollection<Punto2DViewModel>();
            }
            Vertici.Clear();
            if (sezione.Vertici != null)
            {

                foreach (Punto2D puntoIn in sezione.Vertici)
                {
                    Punto2DViewModel puntoVM = new Punto2DViewModel(puntoIn);
                    puntoVM.PropertyChanged += OnEventoModifica;
                    Vertici.Add(puntoVM);
                }
            }
            RaisePropertyChangedEvent("Vertici");
            RaisePropertyChangedEvent("Area");
            RaisePropertyChangedEvent("BaricentroVM");
        }

        public void OnEventoModifica(object sender, PropertyChangedEventArgs e)
        {
            AggiornaProprietà();
        }

        #endregion
    }
}
