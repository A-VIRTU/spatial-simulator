using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Events;

public class SimEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public DateTime Ts { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "Observation"; // "Observation" | "Action" | "Dialogue" | "Reflection" | "StateChange"
    public string? LocationId { get; set; }
    public List<string> Participants { get; set; } = [];
    public string Text { get; set; } = string.Empty;
    public double Importance { get; set; } = 1.0; // 1..10
    public float[]? Embedding { get; set; }
    public List<string>? DerivedFrom { get; set; } // Memory reflection sources
    public ProvenanceComponent? Provenance { get; set; }
}
