namespace SpatialSimulator.Domain.Components;

public enum GenerationState
{
    NotGenerated,
    Outlined,
    Detailed,
    Verified
}

public class GenerationComponent
{
    public GenerationState State { get; set; } = GenerationState.NotGenerated;
    public string? Method { get; set; } // "cadastre" | "osm" | "rule-template" | "llm" | "manual" | "agent-observed"
    public DateTime? GeneratedAt { get; set; }
    public int? ExpectedChildCount { get; set; }
}
