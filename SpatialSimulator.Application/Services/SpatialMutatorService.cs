using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

public interface ISpatialMutatorService
{
    Task<bool> MoveAgentAsync(string agentId, string viaEdgeId);
    Task<bool> TakeItemAsync(string agentId, string itemId);
    Task<bool> PutItemAsync(string itemId, string containerId);
    Task<bool> SetEdgeStateAsync(string edgeId, string state);
}

public class SpatialMutatorService : ISpatialMutatorService
{
    private readonly IWorldRepository _worldRepo;
    private readonly IConnectivityRepository _connectivityRepo;
    private readonly IEventRepository _eventRepo;

    public SpatialMutatorService(
        IWorldRepository worldRepo,
        IConnectivityRepository connectivityRepo,
        IEventRepository eventRepo)
    {
        _worldRepo = worldRepo;
        _connectivityRepo = connectivityRepo;
        _eventRepo = eventRepo;
    }

    public async Task<bool> MoveAgentAsync(string agentId, string viaEdgeId)
    {
        var agent = await _worldRepo.GetAsync(agentId);
        if (agent == null || agent.Agent == null) return false;

        var edge = await _connectivityRepo.GetAsync(viaEdgeId);
        if (edge == null || edge.State == "Locked") return false;

        string currentLoc = agent.Agent.CurrentLocationId;
        string targetLoc = edge.FromId == currentLoc ? edge.ToId : (edge.ToId == currentLoc && edge.Bidirectional ? edge.FromId : string.Empty);

        if (string.IsNullOrEmpty(targetLoc)) return false;

        var targetNode = await _worldRepo.GetAsync(targetLoc);
        if (targetNode == null) return false;

        agent.Agent.CurrentLocationId = targetLoc;
        agent.Agent.LastActedAt = DateTime.UtcNow;

        await _worldRepo.ReparentAsync(agentId, targetLoc);

        await _eventRepo.AddAsync(new SimEvent
        {
            Kind = "Action",
            LocationId = targetLoc,
            Participants = [agentId],
            Text = $"{agent.Name} přechází z {currentLoc} do {targetNode.Name}.",
            Importance = 3.0,
            Provenance = new ProvenanceComponent { Source = "agent-action", Confidence = 1.0 }
        });

        return true;
    }

    public async Task<bool> TakeItemAsync(string agentId, string itemId)
    {
        var agent = await _worldRepo.GetAsync(agentId);
        var item = await _worldRepo.GetAsync(itemId);
        if (agent == null || item == null) return false;

        string oldLoc = item.ParentId ?? string.Empty;
        await _worldRepo.ReparentAsync(itemId, agentId);

        await _eventRepo.AddAsync(new SimEvent
        {
            Kind = "Action",
            LocationId = agent.Agent?.CurrentLocationId ?? oldLoc,
            Participants = [agentId],
            Text = $"{agent.Name} sebral předmět '{item.Name}'.",
            Importance = 4.0,
            Provenance = new ProvenanceComponent { Source = "agent-action", Confidence = 1.0 }
        });

        return true;
    }

    public async Task<bool> PutItemAsync(string itemId, string containerId)
    {
        var item = await _worldRepo.GetAsync(itemId);
        var container = await _worldRepo.GetAsync(containerId);
        if (item == null || container == null) return false;

        await _worldRepo.ReparentAsync(itemId, containerId);

        await _eventRepo.AddAsync(new SimEvent
        {
            Kind = "Action",
            LocationId = containerId,
            Participants = [item.ParentId ?? containerId],
            Text = $"Předmět '{item.Name}' byl vložen do '{container.Name}'.",
            Importance = 3.0,
            Provenance = new ProvenanceComponent { Source = "system-action", Confidence = 1.0 }
        });

        return true;
    }

    public async Task<bool> SetEdgeStateAsync(string edgeId, string state)
    {
        var edge = await _connectivityRepo.GetAsync(edgeId);
        if (edge == null) return false;

        await _connectivityRepo.UpdateStateAsync(edgeId, state);
        return true;
    }
}
