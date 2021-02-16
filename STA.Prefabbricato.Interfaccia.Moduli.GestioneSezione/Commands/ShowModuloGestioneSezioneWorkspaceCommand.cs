﻿using System;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Practices.Prism.Events;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;

using STA.Interfaccia.Common.Events;

using STA.Geometria.Masse.Views;
using STA.Prefabbricato.Interfaccia.Moduli.GestioneSezione.ViewModels;

namespace STA.Prefabbricato.Interfaccia.Moduli.GestioneSezione.Commands
{
    public class ShowModuloGestioneSezioneWorkspaceCommand : ICommand
    {

        private ModuloGestioneSezioneSelectionViewModel _viewModel;

        public ShowModuloGestioneSezioneWorkspaceCommand(ModuloGestioneSezioneSelectionViewModel viewModel)
        {
            _viewModel = viewModel;
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

            SezioneEditView vista = new SezioneEditView();

            regionManager.AddToRegion("WorkspaceRegion", vista);

        }

        /// <summary>
        /// Callback method invoked when navigation has completed.
        /// </summary>
        /// <param name="result">Provides information about the result of the navigation.</param>
        public void NavigationCompleted(NavigationResult result)
        {
            // Exit if navigation was not successful
            if (result.Result != true) return;

            // Publish ViewRequestedEvent
            var eventAggregator = ServiceLocator.Current.GetInstance<IEventAggregator>();
            var navigationCompletedEvent = eventAggregator.GetEvent<NavigationCompletedEvent>();
            navigationCompletedEvent.Publish("ModuloGestioneTrefolatura");
        }
    }
}
