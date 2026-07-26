using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

/// <summary>
/// Rozhraní mutátoru prostory pro manipulaci se stavy entit a pozicemi v simulaci.
/// </summary>
public interface ISpatialMutatorService
{
    /// <summary>
    /// Přemístí agenta do nové lokace a zaznamená událost v GTU logu.
    /// </summary>
    Task<bool> MoveAgentAsync(string agentId, string destinationLocationId);

    /// <summary>
    /// Zvedne/přebere předmět z lokace do vlastnictví/kapsy agenta.
    /// </summary>
    Task<bool> TakeItemAsync(string agentId, string itemId);
}

/// <summary>
/// Mutátor prostorových vztahů provádějící změny v hierarchii a záznam událostí v GTU logu.
/// Motivace: Zabezpečuje atomické změny polohy agentů a předmětů v databázi včetně vygenerování vzpomínek.
/// </summary>
public class SpatialMutatorService : ISpatialMutatorService
{
    private readonly IWorldRepository _worldRepository;
    private readonly IConnectivityRepository _connectivityRepository;
    private readonly IEventRepository _eventRepository;

    /// <summary>
    /// Konstruktor přijímající repozitáře.
    /// </summary>
    public SpatialMutatorService(IWorldRepository worldRepository, IConnectivityRepository connectivityRepository, IEventRepository eventRepository)
    {
        _worldRepository = worldRepository;
        _connectivityRepository = connectivityRepository;
        _eventRepository = eventRepository;
    }

    /// <summary>
    /// Přemístí agenta a vytvoří událost přemístění.
    /// </summary>
    public async Task<bool> MoveAgentAsync(string agentId, string destinationLocationId)
    {
        var agent = await _worldRepository.GetAsync(agentId);
        var dest = await _worldRepository.GetAsync(destinationLocationId);
        if (agent == null || dest == null) return false;

        string oldLocationId = agent.Agent?.CurrentLocationId ?? agent.ParentId ?? "";
        var oldLoc = await _worldRepository.GetAsync(oldLocationId);

        await _worldRepository.ReparentAsync(agentId, destinationLocationId);
        if (agent.Agent != null)
        {
            agent.Agent.CurrentLocationId = destinationLocationId;
            agent.Agent.LastActedAt = DateTime.UtcNow;
            await _worldRepository.ReplaceAsync(agent);
        }

        await _eventRepository.AddAsync(new SimEvent
        {
            Kind = "Action",
            LocationId = destinationLocationId,
            Participants = [agentId],
            Text = $"{agent.Name} přešel(a) z {oldLoc?.Name ?? oldLocationId} do {dest.Name}.",
            Importance = 6.0,
            Ts = DateTime.UtcNow
        });

        return true;
    }

    /// <summary>
    /// Zvedne předmět do vlastnictví agenta.
    /// </summary>
    public async Task<bool> TakeItemAsync(string agentId, string itemId)
    {
        var agent = await _worldRepository.GetAsync(agentId);
        var item = await _worldRepository.GetAsync(itemId);
        if (agent == null || item == null) return false;

        await _worldRepository.ReparentAsync(itemId, agentId);

        await _eventRepository.AddAsync(new SimEvent
        {
            Kind = "Action",
            LocationId = agent.Agent?.CurrentLocationId ?? agent.ParentId,
            Participants = [agentId],
            Text = $"{agent.Name} sebral předmět {item.Name}.",
            Importance = 7.0,
            Ts = DateTime.UtcNow
        });

        return true;
    }
}
