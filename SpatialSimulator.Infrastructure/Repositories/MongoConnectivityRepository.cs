using MongoDB.Driver;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Infrastructure.Repositories;

/// <summary>
/// MongoDB implementace repozitáře konektivity.
/// Motivace: Ukládá hrany grafu přechodů v MongoDB s indexy na FromId a ToId.
/// </summary>
public class MongoConnectivityRepository : IConnectivityRepository
{
    private readonly MongoDbContext _context;

    /// <summary>
    /// Konstruktor přijímající MongoDbContext.
    /// </summary>
    public MongoConnectivityRepository(MongoDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<ConnectivityEdge?> GetAsync(string id)
    {
        return await _context.Edges.Find(e => e.Id == id).FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConnectivityEdge>> GetAllEdgesAsync()
    {
        return await _context.Edges.Find(_ => true).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task AddAsync(ConnectivityEdge edge)
    {
        await _context.Edges.InsertOneAsync(edge);
    }

    /// <inheritdoc/>
    public async Task AddManyAsync(IEnumerable<ConnectivityEdge> edges)
    {
        var list = edges.ToList();
        if (list.Count > 0)
        {
            await _context.Edges.InsertManyAsync(list);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStateAsync(string edgeId, string state)
    {
        var update = Builders<ConnectivityEdge>.Update.Set(e => e.State, state);
        await _context.Edges.UpdateOneAsync(e => e.Id == edgeId, update);
    }
}

/// <summary>
/// MongoDB implementace repozitáře simulovaných událostí (GTU Stream).
/// Motivace: Zajišťuje ukládání a dotazování nad paměťovými streamy agentů.
/// </summary>
public class MongoEventRepository : IEventRepository
{
    private readonly MongoDbContext _context;

    /// <summary>
    /// Konstruktor přijímající MongoDbContext.
    /// </summary>
    public MongoEventRepository(MongoDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task AddAsync(SimEvent simEvent)
    {
        await _context.Events.InsertOneAsync(simEvent);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SimEvent>> GetEventsForAgentAsync(string agentId, int limit = 100)
    {
        return await _context.Events
            .Find(e => e.Participants.Contains(agentId))
            .SortByDescending(e => e.Ts)
            .Limit(limit)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SimEvent>> GetEventsForLocationAsync(string locationId, int limit = 100)
    {
        return await _context.Events
            .Find(e => e.LocationId == locationId)
            .SortByDescending(e => e.Ts)
            .Limit(limit)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SimEvent>> GetAllEventsAsync(int limit = 500)
    {
        return await _context.Events
            .Find(_ => true)
            .SortByDescending(e => e.Ts)
            .Limit(limit)
            .ToListAsync();
    }
}
