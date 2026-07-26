using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Graph;

public class ConnectivityEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string Kind { get; set; } = "Door"; // "Door" | "Corridor" | "Stairs" | "Path" | "Road" | "Window" | "Gate"
    public bool Bidirectional { get; set; } = true;
    public double CostMeters { get; set; } = 1.0;
    public double? CostSeconds { get; set; }
    public string State { get; set; } = "Open"; // "Open" | "Closed" | "Locked"
    public List<string>? RequiredConditions { get; set; }
    public ProvenanceComponent? Provenance { get; set; }
}
