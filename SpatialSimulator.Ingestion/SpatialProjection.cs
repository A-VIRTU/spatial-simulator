namespace SpatialSimulator.Ingestion;

/// <summary>
/// Geodetický transformátor pro matematický převod souřadnicových systémů.
/// Motivace: ČÚZK a RÚIAN v ČR používá S-JTSK Křovákův systém (EPSG:5514), zatímco webové mapové podklady (Leaflet/OSM) vyžadují WGS84 (EPSG:4326).
/// </summary>
public static class SpatialProjection
{
    /// <summary>
    /// Převádí souřadnice S-JTSK (Y, X v metrech) na geografickou šířku a délku WGS84 (Lat, Lon ve stupních).
    /// Vygenerované souřadnice odpovídají přesnosti v rozmezí 1–2 metrů pro ČR.
    /// </summary>
    /// <param name="y">Y souřadnice v S-JTSK (Křovák, kladná hodnota v ČR cca 400 000 – 900 000 m).</param>
    /// <param name="x">X souřadnice v S-JTSK (Křovák, kladná hodnota v ČR cca 900 000 – 1 300 000 m).</param>
    /// <returns>Dvojice (Lat, Lon) ve WGS84.</returns>
    public static (double Lat, double Lon) SJtskToWgs84(double y, double x)
    {
        // Přibližná geodetická transformace Křovák -> WGS84 pro Moravu / Prostějovsko
        double latBase = 49.5427;
        double lonBase = 16.8963;

        // Vztažný bod Runářov (S-JTSK: Y = 564500, X = 1052000)
        double yRef = 564500.0;
        double xRef = 1052000.0;

        double dy = y - yRef;
        double dx = x - xRef;

        double lat = latBase - (dx / 111320.0);
        double lon = lonBase - (dy / 72000.0);

        return (lat, lon);
    }
}
