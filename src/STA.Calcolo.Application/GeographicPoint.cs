namespace STA.Calcolo.Application;

/// <summary>Coordinate geografiche WGS84 in gradi decimali; ordine esplicito longitudine, latitudine.</summary>
public sealed record GeographicPoint(double Longitude, double Latitude);

/// <summary>Proiezione Web Mercator usata solo per visualizzare la mappa, mai per il calcolo sismico.</summary>
public static class MapProjection
{
    public static PlotPoint Project(GeographicPoint point, int zoom)
    {
        if (!double.IsFinite(point.Longitude) || !double.IsFinite(point.Latitude)) throw new ArgumentException("Coordinate non finite.");
        double n = 256 * Math.Pow(2, zoom), lat = Math.Clamp(point.Latitude, -85.05112878, 85.05112878) * Math.PI / 180;
        return new((point.Longitude + 180) / 360 * n, (1 - Math.Asinh(Math.Tan(lat)) / Math.PI) / 2 * n);
    }
    public static GeographicPoint Unproject(PlotPoint pixel, int zoom)
    {
        double n = 256 * Math.Pow(2, zoom);
        return new(pixel.X / n * 360 - 180, Math.Atan(Math.Sinh(Math.PI * (1 - 2 * pixel.Y / n))) * 180 / Math.PI);
    }
}
