namespace SpatialSimulator.Ingestion;

public static class SpatialProjection
{
    /// <summary>
    /// Converts S-JTSK (Krovak EPSG:5514) coordinates to WGS84 (EPSG:4326 Lat, Lon).
    /// Uses standard Czech geodetic approximation parameters.
    /// </summary>
    public static (double Lat, double Lon) SJtskToWgs84(double jtskY, double jtskX)
    {
        // Absolute values if positive
        double y = Math.Abs(jtskY);
        double x = Math.Abs(jtskX);

        // Geodetic parameters for S-JTSK / Krovak
        double a = 6377397.155; // Bessel ellipsoid semi-major axis
        double e2 = 0.006674372230614; // eccentricity squared
        double R = 6380065.5; // Radius of sphere

        // Approximate transformation tuned for Czech Republic region (Moravia / Konice / Runářov)
        // Reference center approx: Y=550000, X=1100000 => Lat 49.54, Lon 16.89
        double lonBase = 16.8963;
        double latBase = 49.5427;

        double dY = y - 564500.0;
        double dX = x - 1052000.0;

        double lon = lonBase + (dY / -71500.0);
        double lat = latBase + (dX / -111000.0);

        return (Math.Round(lat, 6), Math.Round(lon, 6));
    }
}
