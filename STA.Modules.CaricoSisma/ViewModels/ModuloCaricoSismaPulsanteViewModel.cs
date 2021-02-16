using System.Windows.Input;
using Microsoft.Practices.Prism.Events;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using STA.Interfaccia.Common.BaseClasses;
using STA.Interfaccia.Common.Events;
using STA.Modules.CaricoSisma.Commands;

namespace STA.Modules.CaricoSisma.ViewModels
{
    public class ModuloCaricoSismaPulsanteViewModel : ViewModelBase, INavigationAware
    {
        

        private bool? p_IsChecked;

        /// <summary>
        /// Default Constructor
        /// </summary>
        public ModuloCaricoSismaPulsanteViewModel()
        {
            this.Initialize();
        }

        public ICommand ShowModuloCaricoSismaView { get; set; }

        /// <summary>
        /// Whether the button is checked (selected).
        /// </summary>
        public bool? IsChecked
        {
            get { return p_IsChecked; }

            set
            {
                base.RaisePropertyChangingEvent("IsChecked");
                p_IsChecked = value;
                base.RaisePropertyChangedEvent("IsChecked");
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            throw new System.NotImplementedException();
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            throw new System.NotImplementedException();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Sets the IsChecked state of the Task Button when navigation is completed.
        /// </summary>
        /// <param name="publisher">The publisher of the event.</param>
        private void OnNavigationCompleted(string publisher)
        {
            // Exit if this module published the event
            if (publisher == "ModuloCaricoSisma") return;

            // Otherwise, uncheck this button
            this.IsChecked = false;
        }

        private void Initialize()
        {
            this.ShowModuloCaricoSismaView = new ShowModuloCaricoSismaViewCommand(this);

            this.IsChecked = false;

            var eventAggregator = ServiceLocator.Current.GetInstance<IEventAggregator>();
            var navigationCompletedEvent = eventAggregator.GetEvent<NavigationCompletedEvent>();
            navigationCompletedEvent.Subscribe(OnNavigationCompleted, ThreadOption.UIThread);
        }
    }
}
