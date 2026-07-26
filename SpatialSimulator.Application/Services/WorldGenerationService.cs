using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

/// <summary>
/// Rozhraní služby pro líné (on-demand) generování detailů prostorového světa.
/// </summary>
public interface IWorldGenerationService
{
    /// <summary>
    /// Zabezpečí vygenerování dětských entit (místností, podlaží) pod zadaným uzlem, pokud ještě nebyly vytvořeny.
    /// </summary>
    Task EnsureChildrenAsync(string entityId);
}

/// <summary>
/// Rozhraní LLM klienta pro generování popisu interiéru v případě záložní generace.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Vygeneruje odpověď LLM modelu na zadaný prompt text.
    /// </summary>
    Task<string> GenerateTextAsync(string prompt);
}

/// <summary>
/// Mock implementace LLM klienta pro testování bez aktivního klíče API.
/// </summary>
public class MockLlmClient : ILlmClient
{
    /// <summary>
    /// Vrátí simulovaný text generovaný LLM modelem.
    /// </summary>
    public Task<string> GenerateTextAsync(string prompt) => Task.FromResult("Vygenerovaný popis místnosti z LLM šablony.");
}

/// <summary>
/// Služba pro on-demand generování podlaží a místností budov.
/// Motivace: Zabezpečuje líné domýšlení interiéru budov až ve chvíli, kdy agent vstoupí do dané budovy.
/// </summary>
public class WorldGenerationService : IWorldGenerationService
{
    private readonly IWorldRepository _worldRepository;
    private readonly IConnectivityRepository _connectivityRepository;
    private readonly ILlmClient? _llmClient;

    /// <summary>
    /// Konstruktor přijímající repozitáře a nepovinného LLM klienta.
    /// </summary>
    public WorldGenerationService(IWorldRepository worldRepository, IConnectivityRepository connectivityRepository, ILlmClient? llmClient = null)
    {
        _worldRepository = worldRepository;
        _connectivityRepository = connectivityRepository;
        _llmClient = llmClient;
    }

    /// <summary>
    /// Zabezpečí vygenerování obsahu entity na vyžádání.
    /// </summary>
    public async Task EnsureChildrenAsync(string entityId)
    {
        var entity = await _worldRepository.GetAsync(entityId);
        if (entity == null || entity.Generation.State != GenerationState.NotGenerated) return;

        if (entity.Type == SpatialEntityTypes.Building)
        {
            await GenerateBuildingFloorsAsync(entity);
        }
        else if (entity.Type == SpatialEntityTypes.Floor)
        {
            await GenerateFloorRoomsAsync(entity);
        }

        entity.Generation.State = GenerationState.Outlined;
        entity.Generation.GeneratedAt = DateTime.UtcNow;
        await _worldRepository.ReplaceAsync(entity);
    }

    private async Task GenerateBuildingFloorsAsync(SpatialEntity building)
    {
        int floorsCount = 1;
        if (building.Semantic.Attributes.TryGetValue("floors", out var fVal) && fVal is int fInt)
        {
            floorsCount = fInt;
        }

        for (int i = 1; i <= floorsCount; i++)
        {
            var floor = new SpatialEntity
            {
                Id = $"floor_{building.Id}_{i}",
                Type = SpatialEntityTypes.Floor,
                Name = $"{i}. NP (Podlaží {i})",
                ParentId = building.Id,
                Spatial = new SpatialComponent { Frame = "Local", LocalBoundingBox = new BoundingBox3D { X = 0, Y = 0, Z = (i - 1) * 3, W = 10, H = 3, D = 8 } },
                Semantic = new SemanticComponent { Description = $"Podlaží č. {i} budovy {building.Name}." },
                Generation = new GenerationComponent { State = GenerationState.NotGenerated, Method = "rule-template" }
            };

            await _worldRepository.AddAsync(floor);
        }
    }

    private async Task GenerateFloorRoomsAsync(SpatialEntity floor)
    {
        var rooms = new[]
        {
            new { Id = $"room_{floor.Id}_kitchen", Name = "Kuchyň", Tag = "kitchen" },
            new { Id = $"room_{floor.Id}_corridor", Name = "Chodba", Tag = "corridor" },
            new { Id = $"room_{floor.Id}_living", Name = "Obývací pokoj", Tag = "living_room" },
            new { Id = $"room_{floor.Id}_bedroom", Name = "Ložnice", Tag = "bedroom" }
        };

        foreach (var r in rooms)
        {
            var roomEntity = new SpatialEntity
            {
                Id = r.Id,
                Type = SpatialEntityTypes.Room,
                Name = r.Name,
                ParentId = floor.Id,
                Semantic = new SemanticComponent
                {
                    Tags = ["room", r.Tag],
                    Description = $"Místnost {r.Name} vygenerovaná pravidlovou šablonou."
                },
                Generation = new GenerationComponent { State = GenerationState.Detailed, Method = "rule-template" }
            };

            await _worldRepository.AddAsync(roomEntity);
        }

        // Vytvoření dveří mezi chodbou a ostatními místnostmi
        string corridorId = $"room_{floor.Id}_corridor";
        foreach (var r in rooms)
        {
            if (r.Id == corridorId) continue;
            await _connectivityRepository.AddAsync(new ConnectivityEdge
            {
                Id = $"edge_door_{corridorId}_{r.Id}",
                FromId = corridorId,
                ToId = r.Id,
                Kind = "Door",
                CostMeters = 1.5,
                State = "Open"
            });
        }
    }
}
