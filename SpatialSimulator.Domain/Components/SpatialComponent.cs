namespace SpatialSimulator.Domain.Components;

/// <summary>
/// Druh geometrického vymezení objektu v prostoru.
/// </summary>
public enum GeometryKind
{
    /// <summary>Bez geometrie (drobné předměty, obsah kapes).</summary>
    None,

    /// <summary>Bodové ukotvení (střed budovy, POI).</summary>
    Point,

    /// <summary>Lokální 3D kvádr (místnost, nábytek).</summary>
    Box,

    /// <summary>Liniová geometrie (potok, cesta, plot).</summary>
    Polyline,

    /// <summary>Plošný polygon (půdorys budovy, parcela, rybník, les).</summary>
    Polygon
}

/// <summary>
/// Komponenta reprezentující prostorovou geometrii a souřadnicový rámec entit.
/// Motivace: Podporuje světové GPS ukotvení (WGS84), lokální souřadnice vůči rodiči i liniové prvky (potoky).
/// </summary>
public class SpatialComponent
{
    /// <summary>Souřadnicový rámec ("World" nebo "Local").</summary>
    public string Frame { get; set; } = "World";

    /// <summary>Druh geometrie uzlu.</summary>
    public GeometryKind Kind { get; set; } = GeometryKind.Box;

    /// <summary>Světové ukotvení (GPS lon/lat, výška, půdorysný polygon).</summary>
    public GeoAnchor? GlobalAnchor { get; set; }

    /// <summary>Lokální 3D bounding box v metrech vůči rodiči.</summary>
    public BoundingBox3D? LocalBoundingBox { get; set; }

    /// <summary>Světová lomená čára s šířkou v metrech pro liniové prvky (potok, ulica, plot).</summary>
    public WorldPolyline? GlobalPolyline { get; set; }
}

/// <summary>
/// Geografické ukotvení ve světových souřadnicích (WGS84).
/// </summary>
public class GeoAnchor
{
    /// <summary>Zeměpisná délka (Longitude WGS84).</summary>
    public double Lon { get; set; }

    /// <summary>Zeměpisná šířka (Latitude WGS84).</summary>
    public double Lat { get; set; }

    /// <summary>Nadmorská výška v metrech.</summary>
    public double? ElevationM { get; set; }

    /// <summary>Půdorysný polygon pro 2dsphere indexaci.</summary>
    public List<List<double>>? FootprintCoordinates { get; set; }
}

/// <summary>
/// Liniová geometrie ve světových souřadnicích (pro potoky, cesty, ploty).
/// </summary>
public class WorldPolyline
{
    /// <summary>Seznam dvojic [Lon, Lat] tvořících lomenou čáru.</summary>
    public List<List<double>> Coordinates { get; set; } = [];

    /// <summary>Přibližná šířka liniového prvku v metrech (koryto potoka, cesta).</summary>
    public double? WidthM { get; set; }
}

/// <summary>
/// Lokální 3D kvádr pro objekty uvnitř rodičovského kontejneru.
/// </summary>
public class BoundingBox3D
{
    /// <summary>Pozice X v metrech vůči rodiči.</summary>
    public double X { get; set; }

    /// <summary>Pozice Y v metrech vůči rodiči.</summary>
    public double Y { get; set; }

    /// <summary>Pozice Z v metrech vůči rodiči.</summary>
    public double Z { get; set; }

    /// <summary>Šířka v metrech.</summary>
    public double W { get; set; }

    /// <summary>Výška v metrech.</summary>
    public double H { get; set; }

    /// <summary>Hloubka v metrech.</summary>
    public double D { get; set; }

    /// <summary>Natočení ve stupních.</summary>
    public double RotationDeg { get; set; }
}

/// <summary>
/// Nehierarchický prostorový vztah mezi entitami (třetí struktura vedle stromu a grafu).
/// Motivace: Reprezentuje m:n fyzické dotyky/překryvy (např. úsek potoka hraničí s parcelou X).
/// </summary>
public class SpatialRelation
{
    /// <summary>Druh vztahu ("OverlapsWith" | "AdjacentTo" | "BorderedBy" | "CrossesUnder" | "PartOfNetwork").</summary>
    public string Kind { get; set; } = "BorderedBy";

    /// <summary>Identifikátor cílové entity.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Podíl plochy nebo délky v rozsahu 0..1 (pokud je relevantní).</summary>
    public double? OverlapFraction { get; set; }

    /// <summary>Textová poznámka (např. "levý břeh").</summary>
    public string? Note { get; set; }
}
