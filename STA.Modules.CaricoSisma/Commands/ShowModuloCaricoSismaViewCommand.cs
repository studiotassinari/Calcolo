using System;
using System.Windows.Input;
using Microsoft.Practices.Prism.Events;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using STA.Interfaccia.Common.Events;
using STA.Modules.CaricoSisma.ViewModels;


namespace STA.Modules.CaricoSisma.Commands
{
    class ShowModuloCaricoSismaViewCommand : ICommand
    {
        private ModuloCaricoSismaPulsanteViewModel m_ViewModel;


        public ShowModuloCaricoSismaViewCommand(ModuloCaricoSismaPulsanteViewModel viewModel)
        {
            m_ViewModel = viewModel;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            // Initialize
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();

            // Show Ribbon Tab
            var moduleSismaRibbonTab = new Uri("ModuloCaricoSismaRibbonTab", UriKind.Relative);
            regionManager.RequestNavigate("RibbonRegion", moduleSismaRibbonTab);

            // Show Navigator
            //var moduleANavigator = new Uri("ModuleANavigator", UriKind.Relative);
            //regionManager.RequestNavigate("NavigatorRegion", moduleANavigator);

            /* We invoke the NavigationCompleted() callback 
             * method in our final  navigation request. */

            // Show Workspace
            var moduloCaricoSismaWorkspace = new Uri("ModuloCaricoSismaWorkSpace", UriKind.Relative);
            regionManager.RequestNavigate("WorkspaceRegion", moduloCaricoSismaWorkspace, NavigationCompleted);
        }

        /// <summary>
        /// Callback method invoked when navigation has completed.
        /// </summary>
        /// <param name="result">Provides information about the result of the navigation.</param>
        private void NavigationCompleted(NavigationResult result)
        {
            // Exit if navigation was not successful
            if (result.Result != true) return;

            // Publish ViewRequestedEvent
            var eventAggregator = ServiceLocator.Current.GetInstance<IEventAggregator>();
            var navigationCompletedEvent = eventAggregator.GetEvent<NavigationCompletedEvent>();
            navigationCompletedEvent.Publish("ModuloCaricoSisma");
        }
    }
}
