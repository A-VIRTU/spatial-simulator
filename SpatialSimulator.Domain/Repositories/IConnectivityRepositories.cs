using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;

namespace SpatialSimulator.Domain.Repositories;

public interface IConnectivityRepository
{
    Task<ConnectivityEdge?> GetAsync(string id);
    Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId);
    Task<IReadOnlyList<ConnectivityEdge>> GetAllEdgesAsync();
    Task AddAsync(ConnectivityEdge edge);
    Task AddManyAsync(IEnumerable<ConnectivityEdge> edges);
    Task UpdateStateAsync(string edgeId, string state);
}

public interface IEventRepository
{
    Task AddAsync(SimEvent simEvent);
    Task<IReadOnlyList<SimEvent>> GetEventsForAgentAsync(string agentId, int limit = 100);
    Task<IReadOnlyList<SimEvent>> GetEventsForLocationAsync(string locationId, int limit = 100);
    Task<IReadOnlyList<SimEvent>> GetAllEventsAsync(int limit = 500);
}
