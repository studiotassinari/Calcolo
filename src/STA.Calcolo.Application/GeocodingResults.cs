using System.Globalization;
using System.Text.Json;
namespace STA.Calcolo.Application;

/// <summary>Luogo trovato dalla ricerca per indirizzo: nome descrittivo e coordinate WGS84.</summary>
public sealed record GeographicPlace(string Name, GeographicPoint Point);

/// <summary>
/// Parte portabile della ricerca indirizzi con Nominatim (OpenStreetMap): costruzione dell'URI e lettura della risposta JSON.
/// La richiesta HTTP è nel servizio desktop; qui nessun accesso alla rete, così i test non contattano il servizio.
/// Politica d'uso: ricerca solo su comando esplicito (nessun completamento automatico), massimo una richiesta al secondo.
/// </summary>
public static class GeocodingResults
{
    public static Uri SearchUri(string query) =>
        new("https://nominatim.openstreetmap.org/search?format=jsonv2&limit=8&countrycodes=it&accept-language=it&q=" + Uri.EscapeDataString(query.Trim()));

    public static IReadOnlyList<GeographicPlace> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
        var places = new List<GeographicPlace>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("lat", out var lat) || !item.TryGetProperty("lon", out var lon)) continue;
            if (!double.TryParse(lat.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !double.TryParse(lon.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) continue;
            var name = item.TryGetProperty("display_name", out var n) ? n.GetString() ?? "" : "";
            places.Add(new(name, new(x, y)));
        }
        return places;
    }
}
