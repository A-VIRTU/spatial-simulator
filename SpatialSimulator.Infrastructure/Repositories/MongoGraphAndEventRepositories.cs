using MongoDB.Driver;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Infrastructure.Repositories;

public class MongoConnectivityRepository : IConnectivityRepository
{
    private readonly MongoDbContext _context;

    public MongoConnectivityRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<ConnectivityEdge?> GetAsync(string id)
    {
        return await _context.Edges.Find(e => e.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId)
    {
        var filter = Builders<ConnectivityEdge>.Filter.Or(
            Builders<ConnectivityEdge>.Filter.Eq(e => e.FromId, nodeId),
            Builders<ConnectivityEdge>.Filter.And(
                Builders<ConnectivityEdge>.Filter.Eq(e => e.ToId, nodeId),
                Builders<ConnectivityEdge>.Filter.Eq(e => e.Bidirectional, true)
            )
        );
        return await _context.Edges.Find(filter).ToListAsync();
    }

    public async Task<IReadOnlyList<ConnectivityEdge>> GetAllEdgesAsync()
    {
        return await _context.Edges.Find(_ => true).ToListAsync();
    }

    public async Task AddAsync(ConnectivityEdge edge)
    {
        await _context.Edges.InsertOneAsync(edge);
    }

    public async Task AddManyAsync(IEnumerable<ConnectivityEdge> edges)
    {
        var list = edges.ToList();
        if (list.Count > 0)
        {
            await _context.Edges.InsertManyAsync(list);
        }
    }

    public async Task UpdateStateAsync(string edgeId, string state)
    {
        var update = Builders<ConnectivityEdge>.Update.Set(e => e.State, state);
        await _context.Edges.UpdateOneAsync(e => e.Id == edgeId, update);
    }
}

public class MongoEventRepository : IEventRepository
{
    private readonly MongoDbContext _context;

    public MongoEventRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SimEvent simEvent)
    {
        await _context.Events.InsertOneAsync(simEvent);
    }

    public async Task<IReadOnlyList<SimEvent>> GetEventsForAgentAsync(string agentId, int limit = 100)
    {
        return await _context.Events.Find(e => e.Participants.Contains(agentId))
            .SortByDescending(e => e.Ts)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SimEvent>> GetEventsForLocationAsync(string locationId, int limit = 100)
    {
        return await _context.Events.Find(e => e.LocationId == locationId)
            .SortByDescending(e => e.Ts)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SimEvent>> GetAllEventsAsync(int limit = 500)
    {
        return await _context.Events.Find(_ => true)
            .SortByDescending(e => e.Ts)
            .Limit(limit)
            .ToListAsync();
    }
}
