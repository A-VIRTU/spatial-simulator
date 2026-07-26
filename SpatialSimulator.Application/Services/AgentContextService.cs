using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

public record AgentContext(
    SpatialEntity Agent,
    SpatialEntity? CurrentLocation,
    IReadOnlyList<SpatialEntity> Ancestors,
    IReadOnlyList<SpatialEntity> VisibleEntities,
    IReadOnlyList<ConnectivityEdge> AvailableExits,
    IReadOnlyList<RetrievedMemory> RelevantMemories
);

public interface IAgentContextService
{
    Task<AgentContext> BuildAgentContextAsync(string agentId);
}

public class AgentContextService : IAgentContextService
{
    private readonly IWorldRepository _worldRepo;
    private readonly IConnectivityRepository _connectivityRepo;
    private readonly IWorldGenerationService _generationService;
    private readonly IAgentMemoryService _memoryService;

    public AgentContextService(
        IWorldRepository worldRepo,
        IConnectivityRepository connectivityRepo,
        IWorldGenerationService generationService,
        IAgentMemoryService memoryService)
    {
        _worldRepo = worldRepo;
        _connectivityRepo = connectivityRepo;
        _generationService = generationService;
        _memoryService = memoryService;
    }

    public async Task<AgentContext> BuildAgentContextAsync(string agentId)
    {
        var agent = await _worldRepo.GetAsync(agentId);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID '{agentId}' not found.");
        }

        string currentLocationId = agent.Agent?.CurrentLocationId ?? agent.ParentId ?? string.Empty;
        if (string.IsNullOrEmpty(currentLocationId))
        {
            return new AgentContext(agent, null, [], [], [], []);
        }

        await _generationService.EnsureChildrenAsync(currentLocationId);

        var currentLocation = await _worldRepo.GetAsync(currentLocationId);
        var ancestors = await _worldRepo.GetAncestorsAsync(currentLocationId);
        var visibleEntities = await _worldRepo.GetChildrenAsync(currentLocationId);

        var visibleWithoutSelf = visibleEntities.Where(e => e.Id != agentId).ToList();
        var exits = await _connectivityRepo.GetEdgesFromAsync(currentLocationId);

        string situationQuery = $"{currentLocation?.Name} {currentLocation?.Semantic.Description} {agent.Agent?.CurrentGoal}";
        var memories = await _memoryService.RetrieveMemoriesAsync(agentId, situationQuery, topK: 10);

        return new AgentContext(
            agent,
            currentLocation,
            ancestors,
            visibleWithoutSelf,
            exits,
            memories
        );
    }
}
