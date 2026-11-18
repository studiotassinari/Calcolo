using System.Windows;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Views;
namespace STA.Calcolo.Desktop.Services;

/// <summary>Finestre modali richiamate dai ViewModel: restituiscono un risultato senza che il ViewModel conosca WPF.</summary>
public interface IDialogService
{
    /// <summary>Scelta geografica con mappa e ricerca per indirizzo; null se annullata.</summary>
    GeographicPlace? PickLocation(GeographicPoint start, string place);
    /// <summary>Modulo spettri in modalità di selezione: restituisce i valori sismici dello stato limite scelto; null se annullato.</summary>
    SeismicSiteSelection? PickSeismic(SeismicSiteInput? start, string place, string limitState);
}

public sealed class DialogService(IFileService files) : IDialogService
{
    private static Window? Owner() => System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current?.MainWindow;

    public GeographicPlace? PickLocation(GeographicPoint start, string place)
    {
        var vm = new GeoPickerViewModel(new NominatimGeocodingService(), start, place);
        var window = new GeoPickerWindow { DataContext = vm, Owner = Owner() };
        // La chiusura è chiesta dal ViewModel con un evento: nessuna logica nel code-behind.
        vm.CloseRequested += ok => window.DialogResult = ok;
        return window.ShowDialog() == true ? vm.Result : null;
    }

    public SeismicSiteSelection? PickSeismic(SeismicSiteInput? start, string place, string limitState)
    {
        var vm = new SeismicSpectraViewModel(files, this, pickMode: true);
        if (start is not null) vm.Apply(start, place);
        vm.SelectedState = limitState;
        var window = new SeismicSpectraWindow { DataContext = vm, Owner = Owner() };
        vm.CloseRequested += ok => window.DialogResult = ok;
        return window.ShowDialog() == true ? vm.Selection : null;
    }
}
