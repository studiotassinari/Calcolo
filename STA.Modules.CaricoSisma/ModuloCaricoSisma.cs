using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using STA.Modules.CaricoSisma.Views;
using STA.Carichi.Sisma;

namespace STA.Modules.CaricoSisma
{
    public class ModuloCaricoSisma : IModule
    {
        public static Sisma SismaAssociato = new Sisma(12.19944,﻿44.41778);

        public void Initialize()
        {
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            regionManager.RegisterViewWithRegion("TaskButtonRegion", typeof(ModuloSismaPulsante));

            var container = ServiceLocator.Current.GetInstance<IUnityContainer>();
            container.RegisterType<Object, ModuloCaricoSismaRibbonTab>("ModuloCaricoSismaRibbonTab");
            container.RegisterType<Object, ModuloCaricoSismaWorkSpace>("ModuloCaricoSismaWorkSpace");
        }
    }
}
