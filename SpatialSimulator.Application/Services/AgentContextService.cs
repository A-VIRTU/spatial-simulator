using System.Text;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

/// <summary>
/// Percepční sémantický kontext agenta sestavený pro vložení do promptu LLM agentního rozhodování.
/// </summary>
public class AgentPerceptionContext
{
    /// <summary>
    /// ID agenta.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Lidsky čitelný název agenta.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Aktuální sledovaný cíl agenta.
    /// </summary>
    public string CurrentGoal { get; set; } = string.Empty;

    /// <summary>
    /// Aktuální lokace agenta.
    /// </summary>
    public SpatialEntity? CurrentLocation { get; set; }

    /// <summary>
    /// Seznam předmětů a objektů viditelných v bezprostředním okolí agenta.
    /// </summary>
    public List<SpatialEntity> VisibleEntities { get; set; } = [];

    /// <summary>
    /// Seznam dostupných přechodů a východů (dveře, chodby, cesty).
    /// </summary>
    public List<string> AvailableExits { get; set; } = [];

    /// <summary>
    /// Seznam relevantních paměťových vzpomínek vyvolaných pro aktuální situaci.
    /// </summary>
    public List<ScoredMemory> RetrievedMemories { get; set; } = [];

    /// <summary>
    /// Vygenerovaný prompt text pro LLM.
    /// </summary>
    public string PromptText { get; set; } = string.Empty;
}

/// <summary>
/// Rozhraní pro sestavování percepčního kontextu agentů.
/// </summary>
public interface IAgentContextService
{
    /// <summary>
    /// Sestaví kompletní percepční kontext pro daného agenta.
    /// </summary>
    Task<AgentPerceptionContext> BuildAgentContextAsync(string agentId);
}

/// <summary>
/// Služba pro sestavování percepčního promptu agenta.
/// Motivace: Agreguje prostory, předměty, východy a paměti do strukturovaného promptu.
/// </summary>
public class AgentContextService : IAgentContextService
{
    private readonly IWorldRepository _worldRepository;
    private readonly IConnectivityRepository _connectivityRepository;
    private readonly IAgentMemoryService _memoryService;

    /// <summary>
    /// Konstruktor přijímající repozitáře a paměťovou službu.
    /// </summary>
    public AgentContextService(IWorldRepository worldRepository, IConnectivityRepository connectivityRepository, IAgentMemoryService memoryService)
    {
        _worldRepository = worldRepository;
        _connectivityRepository = connectivityRepository;
        _memoryService = memoryService;
    }

    /// <summary>
    /// Sestaví percepční kontext a vygeneruje textový prompt pro LLM agenta.
    /// </summary>
    public async Task<AgentPerceptionContext> BuildAgentContextAsync(string agentId)
    {
        var agent = await _worldRepository.GetAsync(agentId);
        if (agent == null) throw new InvalidOperationException($"Agent s ID {agentId} nebyl nalezen.");

        string locationId = agent.Agent?.CurrentLocationId ?? agent.ParentId ?? string.Empty;
        var location = await _worldRepository.GetAsync(locationId);

        var siblings = await _worldRepository.GetChildrenAsync(locationId);
        var visibleEntities = siblings.Where(e => e.Id != agentId).ToList();

        var edges = await _connectivityRepository.GetEdgesFromAsync(locationId);
        var exits = new List<string>();
        foreach (var edge in edges)
        {
            string otherNodeId = edge.FromId == locationId ? edge.ToId : edge.FromId;
            var targetNode = await _worldRepository.GetAsync(otherNodeId);
            exits.Add($"{edge.Kind} -> {targetNode?.Name ?? otherNodeId} (Stav: {edge.State})");
        }

        var memories = await _memoryService.RetrieveMemoriesAsync(agentId, agent.Agent?.CurrentGoal ?? "", topK: 3);

        string locDesc = location?.Semantic?.Description ?? string.Empty;
        string agentGoal = agent.Agent?.CurrentGoal ?? "Žádný stanovený cíl";

        var sb = new StringBuilder();
        sb.AppendLine($"Jsi agent {agent.Name}.");
        sb.AppendLine($"Tvé poslání / cíl: {agentGoal}.");
        sb.AppendLine($"Právě se nacházíš v: {location?.Name ?? "Neznámé místo"} ({locDesc}).");
        sb.AppendLine("Objekty v tvém okolí:");
        foreach (var v in visibleEntities) sb.AppendLine($" - {v.Name} ({v.Type}): {v.Semantic.Description}");
        sb.AppendLine("Dostupné východy a přechody:");
        foreach (var ex in exits) sb.AppendLine($" - {ex}");
        sb.AppendLine("Relevantní vzpomínky z minulosti:");
        foreach (var m in memories) sb.AppendLine($" - {m.Event.Text}");

        return new AgentPerceptionContext
        {
            AgentId = agentId,
            AgentName = agent.Name,
            CurrentGoal = agentGoal,
            CurrentLocation = location,
            VisibleEntities = visibleEntities,
            AvailableExits = exits,
            RetrievedMemories = memories,
            PromptText = sb.ToString()
        };
    }
}
