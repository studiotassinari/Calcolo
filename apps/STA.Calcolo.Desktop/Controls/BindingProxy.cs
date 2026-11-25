using System.Windows;
namespace STA.Calcolo.Desktop.Controls;

/// <summary>
/// Porta il DataContext agli elementi fuori dall'albero visuale, come le colonne di un DataGrid:
/// permette di legarne intestazione e visibilità al ViewModel senza codice nella finestra.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    public object? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
