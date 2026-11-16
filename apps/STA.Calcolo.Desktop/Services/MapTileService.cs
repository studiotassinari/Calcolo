using System.IO;
using System.Net.Http;
namespace STA.Calcolo.Desktop.Services;

/// <summary>Astrazione del servizio geografico: i test forniscono tessere locali senza contattare OSM.</summary>
public interface IMapTileService
{
    Task<byte[]> ReadAsync(int zoom, int x, int y, CancellationToken token);
}

/// <summary>Scarica solo le tessere visibili, con identificazione e cache locale minima di sette giorni.</summary>
public sealed class OsmTileService : IMapTileService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim Gate = new(2);
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TA-Calcolo/1.4 (+https://github.com/studiotassinari/Calcolo)");
        return client;
    }
    public async Task<byte[]> ReadAsync(int zoom, int x, int y, CancellationToken token)
    {
        if (zoom < 3 || zoom > 18 || x < 0 || y < 0 || x >= (1 << zoom) || y >= (1 << zoom)) throw new ArgumentException("Tessera fuori mappa.");
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TA-Calcolo", "MapCache", zoom.ToString(), x.ToString());
        string path = Path.Combine(directory, y + ".png");
        await Gate.WaitAsync(token);
        try
        {
            if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(7))
                return await File.ReadAllBytesAsync(path, token);
            var bytes = await Client.GetByteArrayAsync($"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png", token);
            if (bytes.Length > 2_000_000) throw new IOException("Tessera troppo grande.");
            // La cache non è un download offline: contiene esclusivamente le viste richieste dall'utente.
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, token); File.Move(temp, path, true);
            return bytes;
        }
        finally { Gate.Release(); }
    }
}
