using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;

using STA.Prefabbricato.Interfaccia.Moduli.GestioneSezione.Views;

namespace STA.Prefabbricato.Interfaccia.Moduli.GestioneSezione
{
    public class ModuloGestioneSezione : IModule
    {

        public void Initialize()
        {
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            regionManager.RegisterViewWithRegion("SelectionRegion", typeof(ModuloGestioneSezioneSelectionView));

            var container = ServiceLocator.Current.GetInstance<IUnityContainer>();
            container.RegisterType<Object, Views.ModuloGestioneSezioneEditView>("ModuloGestioneSezioneEditView");

        }
    }
}
