using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;


using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;

using STA.Prefabbricato.TraveIsostatica;
using STA.Prefabbricato.TraveIsostatica.ViewModels;


namespace STA.Prefabbricato.Interfaccia.Moduli.GestioneTraveIsostatica
{
    public class ModuloGestioneTraveIsostatica : IModule
    {
        private static TraveIsostatica.TraveIsostatica Trave = new TraveIsostatica.TraveIsostatica();
        public static TraveIsostaticaViewModel traveIsostaticaViewModel = new TraveIsostaticaViewModel(Trave);

        public void Initialize()
        {

            /// Varie inizializzazione per l'interfaccia prism
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            regionManager.RegisterViewWithRegion("RibbonRegion", typeof(Views.ModuloGestioneTraveIsostaticaMainRibbonTab));

            //MessageBox.Show("Modulo Trave Prefabbricata Inizializzato");
        }
    }
}
