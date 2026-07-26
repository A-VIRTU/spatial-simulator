namespace SpatialSimulator.Domain.Components;

public class SpatialComponent
{
    public string Frame { get; set; } = "World"; // "World" | "Local"
    public GeoAnchor? GlobalAnchor { get; set; }
    public BoundingBox3D? LocalBoundingBox { get; set; }
}

public class GeoAnchor
{
    public double Lon { get; set; }
    public double Lat { get; set; }
    public double? ElevationM { get; set; }
}

public class BoundingBox3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double W { get; set; } // Width [m]
    public double H { get; set; } // Height [m]
    public double D { get; set; } // Depth [m]
    public double RotationDeg { get; set; }
}
