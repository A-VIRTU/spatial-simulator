namespace SpatialSimulator.Domain.Components;

public class SemanticComponent
{
    public List<string> Tags { get; set; } = [];
    public string? Description { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
}
