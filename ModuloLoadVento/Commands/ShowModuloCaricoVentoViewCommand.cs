using System;
using System.Windows.Input;
using Microsoft.Practices.Prism.Events;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using STA.Interfaccia.Common.Events;
using STA.Modules.CaricoVento.ViewModels;

namespace STA.Modules.CaricoVento.Commands
{
    class ShowModuloCaricoVentoViewCommand : ICommand
    {
                        #region Fields

        // Member variables
        private ModuloCaricoVentoPulsanteViewModel m_ViewModel;

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ShowModuloCaricoVentoViewCommand(ModuloCaricoVentoPulsanteViewModel viewModel)
        {
            m_ViewModel = viewModel;
        }

        #endregion

        #region ICommand Members

        /// <summary>
        /// Whether the ShowModuleAViewCommand is enabled.
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return true;
        }

        /// <summary>
        /// Actions to take when CanExecute() changes.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Executes the ShowModuleAViewCommand
        /// </summary>
        public void Execute(object parameter)
        {
            // Initialize
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();

            // Show Ribbon Tab
            //var moduleARibbonTab = new Uri("ModuleARibbonTab", UriKind.Relative);
            //regionManager.RequestNavigate("RibbonRegion", moduleARibbonTab);

            // Show Navigator
            //var moduleANavigator = new Uri("ModuleANavigator", UriKind.Relative);
            //regionManager.RequestNavigate("NavigatorRegion", moduleANavigator);

            /* We invoke the NavigationCompleted() callback 
             * method in our final  navigation request. */

            // Show Workspace
            var moduleAWorkspace = new Uri("ModuloCaricoVentoWorkspace", UriKind.Relative);
            regionManager.RequestNavigate("WorkspaceRegion", moduleAWorkspace, NavigationCompleted);
        }

        #endregion

        #region Private Methods

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
            navigationCompletedEvent.Publish("ModuloCaricoVento");
        }

        #endregion
    }
}
