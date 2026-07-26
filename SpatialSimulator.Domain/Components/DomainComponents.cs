namespace SpatialSimulator.Domain.Components;

public class ProvenanceComponent
{
    public string Source { get; set; } = string.Empty; // "RUIAN" | "OSM" | "Mapillary" | "vision-llm" | "manual"
    public string? SourceRef { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public double Confidence { get; set; } = 1.0; // 0..1
}

public class CapacityComponent
{
    public int? MaxOccupants { get; set; }
    public double? MaxVolumeLiters { get; set; }
    public int? MaxItemCount { get; set; }
}

public class AgentComponent
{
    public string PersonaRef { get; set; } = string.Empty;
    public string CurrentLocationId { get; set; } = string.Empty;
    public string? CurrentGoal { get; set; }
    public DateTime LastActedAt { get; set; } = DateTime.UtcNow;
}
