namespace SpatialSimulator.Domain.Components;

/// <summary>
/// Prostorová komponenta entity určující její geografické nebo lokální souřadnicové vymezení.
/// Motivace: Odděluje fyzickou geometrii od sémantických atributů a umožňuje kombinovat reálné GPS ukotvení budov s relativními bounding boxy místností.
/// </summary>
public class SpatialComponent
{
    /// <summary>
    /// Souřadnicový rámec entity. Nabývá hodnot "World" (globální GPS) nebo "Local" (lokální v metrech vůči rodiči).
    /// </summary>
    public string Frame { get; set; } = "World";

    /// <summary>
    /// Globální geografické ukotvení ve WGS84 (lat/lon). Vyplňuje se pouze u uzlů ukotvených v reálném světě (budova, pozemek, venkovní místo).
    /// </summary>
    public GeoAnchor? GlobalAnchor { get; set; }

    /// <summary>
    /// Lokální 3D bounding box v metrech vůči rodičovskému uzlu. Používá se pro patra, místnosti a nábytek.
    /// </summary>
    public BoundingBox3D? LocalBoundingBox { get; set; }
}

/// <summary>
/// Geografické ukotvení entity v reálném světě.
/// Motivace: Umožňuje přesné zobrazení v mapových podkladech a prostorový index 2dsphere v databázi.
/// </summary>
public class GeoAnchor
{
    /// <summary>
    /// Zeměpisná délka (Longitude) ve WGS84 (EPSG:4326).
    /// </summary>
    public double Lon { get; set; }

    /// <summary>
    /// Zeměpisná šířka (Latitude) ve WGS84 (EPSG:4326).
    /// </summary>
    public double Lat { get; set; }

    /// <summary>
    /// Nadmořská výška v metrech (nepovinná).
    /// </summary>
    public double? ElevationM { get; set; }
}

/// <summary>
/// Lokální trojrozměrný bounding box v metrech.
/// Motivace: Poskytuje přibližný rozměr a orientaci objektu uvnitř rodičovského kontejneru bez nutnosti složité 3D síťové geometrie.
/// </summary>
public class BoundingBox3D
{
    /// <summary>
    /// X pozice počátku vůči rodiči v metrech.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y pozice počátku vůči rodiči v metrech.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Z pozice počátku vůči rodiči v metrech.
    /// </summary>
    public double Z { get; set; }

    /// <summary>
    /// Šířka objektu v metrech (Width).
    /// </summary>
    public double W { get; set; }

    /// <summary>
    /// Výška objektu v metrech (Height).
    /// </summary>
    public double H { get; set; }

    /// <summary>
    /// Hloubka objektu v metrech (Depth).
    /// </summary>
    public double D { get; set; }

    /// <summary>
    /// Úhel natočení ve stupních vůči osám rodiče.
    /// </summary>
    public double RotationDeg { get; set; }
}
