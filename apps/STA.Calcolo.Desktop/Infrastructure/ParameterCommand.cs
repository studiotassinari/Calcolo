using System.Windows.Input;
namespace STA.Calcolo.Desktop.Infrastructure;

/// <summary>Comando con parametro tipizzato: usato dai controlli grafici per inviare richieste al ViewModel.</summary>
public sealed class ParameterCommand<T>(Action<T> action, Func<T,bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => parameter is T value && (canExecute?.Invoke(value) ?? true);
    public void Execute(object? parameter) { if (parameter is T value && CanExecute(value)) action(value); }
    public event EventHandler? CanExecuteChanged;
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
