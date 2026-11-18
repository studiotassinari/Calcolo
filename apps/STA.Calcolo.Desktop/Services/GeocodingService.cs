using System.Net.Http;
using STA.Calcolo.Application;
namespace STA.Calcolo.Desktop.Services;

/// <summary>Ricerca di un sito per indirizzo; sostituibile nei test senza accedere alla rete.</summary>
public interface IGeocodingService
{
    Task<IReadOnlyList<GeographicPlace>> SearchAsync(string query, CancellationToken token);
}

/// <summary>
/// Geocodifica con Nominatim (OpenStreetMap): una richiesta per ogni ricerca confermata dall'utente, al massimo una al secondo,
/// con User-Agent identificativo come richiesto dalla politica d'uso del servizio. Risultati limitati all'Italia.
/// </summary>
public sealed class NominatimGeocodingService : IGeocodingService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim Gate = new(1);
    private static DateTime last = DateTime.MinValue;
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TA-Calcolo/1.8 (+https://github.com/studiotassinari/Calcolo)");
        return client;
    }
    public async Task<IReadOnlyList<GeographicPlace>> SearchAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3) throw new ArgumentException("Inserire almeno tre caratteri dell'indirizzo o del comune.");
        await Gate.WaitAsync(token);
        try
        {
            var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - last);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
            last = DateTime.UtcNow;
            var json = await Client.GetStringAsync(GeocodingResults.SearchUri(query), token);
            return GeocodingResults.Parse(json);
        }
        finally { Gate.Release(); }
    }
}
