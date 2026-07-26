using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Entities;

public class SpatialEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Type { get; set; } = string.Empty; // SpatialEntityTypes
    public string Name { get; set; } = string.Empty;

    // Tree Containment
    public string? ParentId { get; set; }
    public List<string> Ancestors { get; set; } = []; // root..parent
    public string MaterializedPath { get; set; } = string.Empty;
    public int Depth { get; set; }

    // ECS Components
    public SpatialComponent? Spatial { get; set; }
    public SemanticComponent Semantic { get; set; } = new();
    public GenerationComponent Generation { get; set; } = new();
    public ProvenanceComponent? Provenance { get; set; }
    public CapacityComponent? Capacity { get; set; }
    public AgentComponent? Agent { get; set; }

    public Dictionary<string, string> ExternalRefs { get; set; } = new();
    public int SchemaVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
