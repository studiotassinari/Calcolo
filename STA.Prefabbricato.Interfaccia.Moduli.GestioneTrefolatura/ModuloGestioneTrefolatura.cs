using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;


namespace STA.Prefabbricato.Interfaccia.Moduli.GestioneTrefolatura
{
    public class ModuloGestioneTrefolatura : IModule
    {


        public void Initialize()
        {
            /* We register always-available controls with the Prism Region Manager, and on-demand 
             * controls with the DI container. On-demand controls will be loaded when we invoke
             * IRegionManager.RequestNavigate() to load the controls. */

            // Register task button with Prism Region
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            regionManager.RegisterViewWithRegion("SelectionRegion", typeof(Views.ModuloGestioneTrefolaturaSelectionView));

            var container = ServiceLocator.Current.GetInstance<IUnityContainer>();
            container.RegisterType<Object, Views.ModuloGestioneTrefolaturaWorkSpaceView>("ModuloGestioneTrefolaturaWorkSpaceView");
        }
    }
}
