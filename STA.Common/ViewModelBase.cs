﻿using System.ComponentModel;

namespace STA.Common
{
    /// <summary>
    /// ModelView base per le classi viewmodel da implementare
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        #region membri Inotifypropertychanged

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion


        #region Administrative Properties

        /// <summary>
        /// Whether the view model should ignore property-change events.
        /// </summary>
        public virtual bool IgnorePropertyChangeEvents { get; set; }

        #endregion

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        public virtual void RaisePropertyChangedEvent(string propertyName)
        {
            // Exit if changes ignored
            if (IgnorePropertyChangeEvents) return;

            // Exit if no subscribers
            if (PropertyChanged == null) return;

            // Raise event
            var e = new PropertyChangedEventArgs(propertyName);
            PropertyChanged(this, e);
        }
    }
}
