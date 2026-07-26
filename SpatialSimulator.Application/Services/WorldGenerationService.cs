using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

public interface ILlmGenerator
{
    Task<List<ChildSpec>> GenerateChildrenAsync(SpatialEntity container, IReadOnlyList<SpatialEntity> ancestors);
}

public record ChildSpec(string Name, string Type, List<string> Tags, string Description, Dictionary<string, object>? Attributes = null);

public interface IWorldGenerationService
{
    Task EnsureChildrenAsync(string containerId, GenerationState minState = GenerationState.Outlined);
}

public class DefaultLlmGeneratorFallback : ILlmGenerator
{
    public Task<List<ChildSpec>> GenerateChildrenAsync(SpatialEntity container, IReadOnlyList<SpatialEntity> ancestors)
    {
        var results = new List<ChildSpec>();
        if (container.Type == SpatialEntityTypes.Building)
        {
            results.Add(new ChildSpec("Přízemí", SpatialEntityTypes.Floor, ["ground_floor"], "Přízemí budovy"));
        }
        else if (container.Type == SpatialEntityTypes.Floor)
        {
            results.Add(new ChildSpec("Vstupní chodba", SpatialEntityTypes.Room, ["corridor"], "Vstupní chodba"));
            results.Add(new ChildSpec("Hlavní místnost", SpatialEntityTypes.Room, ["living_room"], "Hlavní obytný prostor"));
        }
        else if (container.Type == SpatialEntityTypes.Room)
        {
            results.Add(new ChildSpec("Stůl", SpatialEntityTypes.Furniture, ["table"], "Dřevěný stůl"));
            results.Add(new ChildSpec("Židle", SpatialEntityTypes.Furniture, ["chair"], "Dřevěná židle"));
        }
        return Task.FromResult(results);
    }
}

public class WorldGenerationService : IWorldGenerationService
{
    private readonly IWorldRepository _worldRepo;
    private readonly IConnectivityRepository _connectivityRepo;
    private readonly ILlmGenerator _llmGenerator;

    public WorldGenerationService(
        IWorldRepository worldRepo,
        IConnectivityRepository connectivityRepo,
        ILlmGenerator? llmGenerator = null)
    {
        _worldRepo = worldRepo;
        _connectivityRepo = connectivityRepo;
        _llmGenerator = llmGenerator ?? new DefaultLlmGeneratorFallback();
    }

    public async Task EnsureChildrenAsync(string containerId, GenerationState minState = GenerationState.Outlined)
    {
        var container = await _worldRepo.GetAsync(containerId);
        if (container == null || container.Generation.State >= minState) return;

        var existingChildren = await _worldRepo.GetChildrenAsync(containerId);
        if (existingChildren.Count > 0)
        {
            container.Generation.State = minState;
            await _worldRepo.ReplaceAsync(container);
            return;
        }

        List<ChildSpec> specs = GenerateRuleBased(container);
        if (specs.Count == 0)
        {
            var ancestors = await _worldRepo.GetAncestorsAsync(containerId);
            specs = await _llmGenerator.GenerateChildrenAsync(container, ancestors);
        }

        var newEntities = new List<SpatialEntity>();

        foreach (var spec in specs)
        {
            var child = new SpatialEntity
            {
                Name = spec.Name,
                Type = spec.Type,
                ParentId = containerId,
                Semantic = new SemanticComponent
                {
                    Tags = spec.Tags,
                    Description = spec.Description,
                    Attributes = spec.Attributes ?? new Dictionary<string, object>()
                },
                Generation = new GenerationComponent
                {
                    State = GenerationState.Outlined,
                    Method = "rule-template",
                    GeneratedAt = DateTime.UtcNow
                },
                Provenance = new ProvenanceComponent
                {
                    Source = "rule-template",
                    Confidence = 0.8,
                    ExtractedAt = DateTime.UtcNow
                }
            };

            newEntities.Add(child);
        }

        await _worldRepo.AddManyAsync(newEntities);

        if (container.Type == SpatialEntityTypes.Floor && newEntities.Count > 1)
        {
            var edges = new List<ConnectivityEdge>();
            for (int i = 0; i < newEntities.Count - 1; i++)
            {
                edges.Add(new ConnectivityEdge
                {
                    FromId = newEntities[i].Id,
                    ToId = newEntities[i + 1].Id,
                    Kind = "Door",
                    Bidirectional = true,
                    CostMeters = 2.0,
                    State = "Open",
                    Provenance = new ProvenanceComponent
                    {
                        Source = "rule-template",
                        Confidence = 0.8
                    }
                });
            }
            await _connectivityRepo.AddManyAsync(edges);
        }

        container.Generation.State = minState;
        container.Generation.GeneratedAt = DateTime.UtcNow;
        await _worldRepo.ReplaceAsync(container);
    }

    private static List<ChildSpec> GenerateRuleBased(SpatialEntity container)
    {
        var specs = new List<ChildSpec>();

        if (container.Type == SpatialEntityTypes.Building)
        {
            int floorCount = 1;
            if (container.Semantic.Attributes.TryGetValue("floors", out var fVal) && int.TryParse(fVal.ToString(), out int parsedFloors))
            {
                floorCount = parsedFloors;
            }

            for (int f = 1; f <= floorCount; f++)
            {
                string floorName = f == 1 ? "Přízemí" : $"{f}. patro";
                specs.Add(new ChildSpec(floorName, SpatialEntityTypes.Floor, ["floor"], $"Podlaží {f} budovy {container.Name}"));
            }
        }
        else if (container.Type == SpatialEntityTypes.Floor)
        {
            specs.Add(new ChildSpec("Vstupní chodba", SpatialEntityTypes.Room, ["corridor"], "Vstupní chodba s věšákem"));
            specs.Add(new ChildSpec("Kuchyň", SpatialEntityTypes.Room, ["kitchen"], "Kuchyň s oknem do dvora"));
            specs.Add(new ChildSpec("Obývací pokoj", SpatialEntityTypes.Room, ["living_room"], "Obývací pokoj"));
            specs.Add(new ChildSpec("Ložnice", SpatialEntityTypes.Room, ["bedroom"], "Ložnice"));
        }

        return specs;
    }
}
