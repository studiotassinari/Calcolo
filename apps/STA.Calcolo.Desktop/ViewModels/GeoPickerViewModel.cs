using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
using STA.Calcolo.Desktop.Services;
namespace STA.Calcolo.Desktop.ViewModels;

/// <summary>
/// Scelta geografica del sito: ricerca per indirizzo (servizio sostituibile), marker sulla mappa e coordinate modificabili.
/// La conferma restituisce il luogo (nome e coordinate WGS84 in gradi decimali) al chiamante tramite <see cref="Result"/>.
/// </summary>
public sealed class GeoPickerViewModel : ObservableObject
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private readonly IGeocodingService geocoding;
    private CancellationTokenSource? search;
    private bool syncing;

    public GeoPickerViewModel(IGeocodingService geocoding, GeographicPoint start, string place)
    {
        this.geocoding = geocoding;
        location = start; this.place = place;
        longitude = start.Longitude.ToString("0.000000", It); latitude = start.Latitude.ToString("0.000000", It);
        SearchCommand = new(Search, ex => Status = "Ricerca non riuscita: " + ex.Message);
        ConfirmCommand = new(() => { Result = new(Place, Location); CloseRequested?.Invoke(true); }, () => CoordinatesValid);
        CancelCommand = new(() => CloseRequested?.Invoke(false));
    }

    /// <summary>Richiesta di chiusura della finestra: true = conferma, false = annulla.</summary>
    public event Action<bool>? CloseRequested;
    public GeographicPlace? Result { get; private set; }

    private string query = "";
    public string Query { get => query; set => Set(ref query, value); }
    private IReadOnlyList<GeographicPlace> results = [];
    public IReadOnlyList<GeographicPlace> Results { get => results; private set => Set(ref results, value); }
    private GeographicPlace? selected;
    /// <summary>Un risultato selezionato sposta il marker e ne assume il nome.</summary>
    public GeographicPlace? SelectedResult
    {
        get => selected;
        set { if (Set(ref selected, value) && value is not null) { Place = value.Name; Location = value.Point; } }
    }
    private string place;
    public string Place { get => place; set => Set(ref place, value); }
    private string status = "Cercare un indirizzo o un comune, oppure fare clic sulla mappa.";
    public string Status { get => status; private set => Set(ref status, value); }

    private GeographicPoint location;
    /// <summary>Posizione del marker (binding bidirezionale con la mappa); aggiorna i campi di testo.</summary>
    public GeographicPoint Location
    {
        get => location;
        set
        {
            if (value is null || !Set(ref location, value)) return;
            syncing = true;
            try { Longitude = value.Longitude.ToString("0.000000", It); Latitude = value.Latitude.ToString("0.000000", It); }
            finally { syncing = false; }
            Changed(nameof(CoordinatesText)); ConfirmCommand.Refresh();
        }
    }
    private string longitude, latitude;
    public string Longitude { get => longitude; set { if (Set(ref longitude, value)) FromText(); } }
    public string Latitude { get => latitude; set { if (Set(ref latitude, value)) FromText(); } }
    public string CoordinatesText => CoordinatesValid ? $"lat {location.Latitude.ToString("0.00000", It)}° · lon {location.Longitude.ToString("0.00000", It)}°" : "Coordinate non valide";
    /// <summary>Il reticolo sismico copre lon 6–19°, lat 36–48°: fuori da questo intervallo la conferma è disabilitata.</summary>
    public bool CoordinatesValid => location.Longitude is >= 6 and <= 19 && location.Latitude is >= 36 and <= 48;

    public AsyncCommand SearchCommand { get; }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private void FromText()
    {
        if (syncing) return;
        if (TryNumber(longitude, out var x) && TryNumber(latitude, out var y) && x is >= -180 and <= 180 && y is >= -85 and <= 85)
        { location = new(x, y); Changed(nameof(Location)); Status = CoordinatesValid ? "Coordinate inserite manualmente." : "Coordinate fuori dal reticolo sismico (lon 6–19°, lat 36–48°)."; }
        else Status = "Inserire longitudine e latitudine in gradi decimali.";
        Changed(nameof(CoordinatesText)); ConfirmCommand.Refresh();
    }

    private async Task Search()
    {
        search?.Cancel(); search = new();
        Status = "Ricerca in corso…";
        var found = await geocoding.SearchAsync(Query, search.Token);
        Results = found;
        if (found.Count == 0) { Status = "Nessun risultato: precisare l'indirizzo o fare clic sulla mappa."; return; }
        SelectedResult = found[0];
        Status = found.Count == 1 ? "Trovato un risultato." : $"Trovati {found.Count} risultati: scegliere quello corretto dall'elenco.";
    }
}
