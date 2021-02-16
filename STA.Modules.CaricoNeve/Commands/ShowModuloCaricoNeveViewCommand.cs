using System;
using System.Windows.Input;
using Microsoft.Practices.Prism.Events;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using STA.Interfaccia.Common.Events;
using STA.Modules.CaricoNeve.ViewModels;


namespace STA.Modules.CaricoNeve.Commands
{
    class ShowModuloCaricoNeveViewCommand : ICommand
    {
        private ModuloCaricoNevePulsanteViewModel m_ViewModel;

                /// <summary>
        /// Default constructor.
        /// </summary>
        public ShowModuloCaricoNeveViewCommand(ModuloCaricoNevePulsanteViewModel viewModel)
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
            var regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();

            var moduloCaricoNeveRibbonTab = new Uri("ModuloCaricoNeveRibbonTab", UriKind.Relative);
            regionManager.RequestNavigate("RibbonRegion", moduloCaricoNeveRibbonTab);

            var moduloCaricoNeveWorkspace = new Uri("ModuloCaricoNeveWorkSpace", UriKind.Relative);
            regionManager.RequestNavigate("WorkspaceRegion", moduloCaricoNeveWorkspace, NavigationCompleted);
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
            navigationCompletedEvent.Publish("ModuloCaricoNeve");
        }
    }
}
