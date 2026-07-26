using MongoDB.Bson;
using MongoDB.Driver;
using SpatialSimulator.Agents;
using SpatialSimulator.Api.Hubs;
using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain.Repositories;
using SpatialSimulator.Infrastructure;
using SpatialSimulator.Infrastructure.Repositories;
using SpatialSimulator.Ingestion;

namespace SpatialSimulator.Api;

/// <summary>
/// Hlavní vstupní třída webové aplikace a REST API rozhraní pro Sémantický prostorový simulátor.
/// </summary>
public class Program
{
    /// <summary>
    /// Vstupní bod programu (Main).
    /// </summary>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddSignalR();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials();
            });
        });

        // Database setup with robust MongoDB ping test & fallback to InMemory
        string connectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
        string dbName = "SpatialSimulator_Runarov";

        bool useMongo = false;
        MongoDbContext? dbContext = null;

        try
        {
            var client = new MongoClient(connectionString);
            var pingTask = client.GetDatabase(dbName).RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            if (await Task.WhenAny(pingTask, Task.Delay(2000)) == pingTask)
            {
                await pingTask;
                dbContext = new MongoDbContext(connectionString, dbName);
                await dbContext.EnsureIndexesAsync();
                useMongo = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MongoDB connection ping failed: {ex.Message}. Falling back to InMemory repositories.");
            useMongo = false;
        }

        if (useMongo && dbContext != null)
        {
            Console.WriteLine($"Connected to MongoDB at {connectionString} (Database: {dbName}).");
            builder.Services.AddSingleton(dbContext);
            builder.Services.AddSingleton<IWorldRepository, MongoWorldRepository>();
            builder.Services.AddSingleton<IConnectivityRepository, MongoConnectivityRepository>();
            builder.Services.AddSingleton<IEventRepository, MongoEventRepository>();
        }
        else
        {
            Console.WriteLine("Running with InMemory repositories.");
            var inMemWorld = new InMemoryWorldRepository();
            var inMemConn = new InMemoryConnectivityRepository();
            var inMemEvent = new InMemoryEventRepository();

            builder.Services.AddSingleton<IWorldRepository>(inMemWorld);
            builder.Services.AddSingleton<IConnectivityRepository>(inMemConn);
            builder.Services.AddSingleton<IEventRepository>(inMemEvent);
        }

        // Domain & Application Services
        builder.Services.AddSingleton<IConnectivityGraphService, ConnectivityGraphService>();
        builder.Services.AddSingleton<IWorldGenerationService, WorldGenerationService>();
        builder.Services.AddSingleton<IAgentMemoryService, AgentMemoryService>();
        builder.Services.AddSingleton<IAgentContextService, AgentContextService>();
        builder.Services.AddSingleton<ISpatialMutatorService, SpatialMutatorService>();
        builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
        builder.Services.AddSingleton<AgentLoopDriver>();

        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<SimulationHub>("/hubs/simulation");

        // Pre-seed Real Runářov Geodata from OSM & RÚIAN on startup if empty
        using (var scope = app.Services.CreateScope())
        {
            var worldRepo = scope.ServiceProvider.GetRequiredService<IWorldRepository>();
            var connRepo = scope.ServiceProvider.GetRequiredService<IConnectivityRepository>();
            var graphService = scope.ServiceProvider.GetRequiredService<IConnectivityGraphService>();

            var existing = await worldRepo.GetAsync("settlement_runarov");
            if (existing == null)
            {
                string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
                var realSeeder = new RealRunarovSeeder(worldRepo, connRepo);
                await realSeeder.SeedRealRunarovAsync(dataDir);

                // Generate rooms for Čp. 23
                var genService = scope.ServiceProvider.GetRequiredService<IWorldGenerationService>();
                await genService.EnsureChildrenAsync("floor_building_cp_23_1");
            }
            await graphService.ReloadGraphAsync();
        }

        await app.RunAsync();
    }
}

/// <summary>
/// In-Memory repozitář světa pro testovací účely.
/// </summary>
public class InMemoryWorldRepository : IWorldRepository
{
    private readonly Dictionary<string, Domain.Entities.SpatialEntity> _store = new();
    public Task<Domain.Entities.SpatialEntity?> GetAsync(string id) { _store.TryGetValue(id, out var e); return Task.FromResult(e); }
    public Task<IReadOnlyList<Domain.Entities.SpatialEntity>> GetChildrenAsync(string parentId) => Task.FromResult<IReadOnlyList<Domain.Entities.SpatialEntity>>(_store.Values.Where(e => e.ParentId == parentId).ToList());
    public Task<IReadOnlyList<Domain.Entities.SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null) => Task.FromResult<IReadOnlyList<Domain.Entities.SpatialEntity>>(_store.Values.Where(e => e.Ancestors.Contains(rootId) || e.Id == rootId).ToList());
    public Task<IReadOnlyList<Domain.Entities.SpatialEntity>> GetAncestorsAsync(string id)
    {
        if (!_store.TryGetValue(id, out var entity)) return Task.FromResult<IReadOnlyList<Domain.Entities.SpatialEntity>>([]);
        var list = entity.Ancestors.Select(aId => _store.TryGetValue(aId, out var a) ? a : null!).Where(a => a != null).ToList();
        return Task.FromResult<IReadOnlyList<Domain.Entities.SpatialEntity>>(list);
    }
    public Task AddAsync(Domain.Entities.SpatialEntity entity) { SetHierarchy(entity); _store[entity.Id] = entity; return Task.CompletedTask; }
    public Task AddManyAsync(IEnumerable<Domain.Entities.SpatialEntity> entities) { foreach (var e in entities) AddAsync(e); return Task.CompletedTask; }
    public Task ReplaceAsync(Domain.Entities.SpatialEntity entity) { _store[entity.Id] = entity; return Task.CompletedTask; }
    public Task ReparentAsync(string id, string newParentId)
    {
        if (!_store.TryGetValue(id, out var node) || !_store.TryGetValue(newParentId, out var newParent)) return Task.CompletedTask;
        node.ParentId = newParentId;
        node.Ancestors = newParent.Ancestors.Concat([newParentId]).ToList();
        node.Depth = node.Ancestors.Count;
        node.MaterializedPath = $"{newParent.MaterializedPath}/{node.Id}";
        return Task.CompletedTask;
    }
    public Task DeleteAsync(string id) { _store.Remove(id); return Task.CompletedTask; }
    private void SetHierarchy(Domain.Entities.SpatialEntity entity)
    {
        if (!string.IsNullOrEmpty(entity.ParentId) && _store.TryGetValue(entity.ParentId, out var parent))
        {
            entity.Ancestors = parent.Ancestors.Concat([parent.Id]).ToList();
            entity.Depth = entity.Ancestors.Count;
            entity.MaterializedPath = $"{parent.MaterializedPath}/{entity.Id}";
        }
        else
        {
            entity.Ancestors = [];
            entity.Depth = 0;
            entity.MaterializedPath = $"/{entity.Id}";
        }
    }
}

/// <summary>
/// In-Memory repozitář konektivity pro testovací účely.
/// </summary>
public class InMemoryConnectivityRepository : IConnectivityRepository
{
    private readonly List<Domain.Graph.ConnectivityEdge> _edges = [];
    public Task<Domain.Graph.ConnectivityEdge?> GetAsync(string id) => Task.FromResult(_edges.FirstOrDefault(e => e.Id == id));
    public Task<IReadOnlyList<Domain.Graph.ConnectivityEdge>> GetEdgesFromAsync(string nodeId) => Task.FromResult<IReadOnlyList<Domain.Graph.ConnectivityEdge>>(_edges.Where(e => e.FromId == nodeId || (e.ToId == nodeId && e.Bidirectional)).ToList());
    public Task<IReadOnlyList<Domain.Graph.ConnectivityEdge>> GetAllEdgesAsync() => Task.FromResult<IReadOnlyList<Domain.Graph.ConnectivityEdge>>(_edges);
    public Task AddAsync(Domain.Graph.ConnectivityEdge edge) { _edges.Add(edge); return Task.CompletedTask; }
    public Task AddManyAsync(IEnumerable<Domain.Graph.ConnectivityEdge> edges) { _edges.AddRange(edges); return Task.CompletedTask; }
    public Task UpdateStateAsync(string edgeId, string state) { var e = _edges.FirstOrDefault(x => x.Id == edgeId); if (e != null) e.State = state; return Task.CompletedTask; }
}

/// <summary>
/// In-Memory repozitář událostí pro testovací účely.
/// </summary>
public class InMemoryEventRepository : IEventRepository
{
    private readonly List<Domain.Events.SimEvent> _events = [];
    public Task AddAsync(Domain.Events.SimEvent simEvent) { _events.Add(simEvent); return Task.CompletedTask; }
    public Task<IReadOnlyList<Domain.Events.SimEvent>> GetEventsForAgentAsync(string agentId, int limit = 100) => Task.FromResult<IReadOnlyList<Domain.Events.SimEvent>>(_events.Where(e => e.Participants.Contains(agentId)).OrderByDescending(e => e.Ts).Take(limit).ToList());
    public Task<IReadOnlyList<Domain.Events.SimEvent>> GetEventsForLocationAsync(string locationId, int limit = 100) => Task.FromResult<IReadOnlyList<Domain.Events.SimEvent>>(_events.Where(e => e.LocationId == locationId).OrderByDescending(e => e.Ts).Take(limit).ToList());
    public Task<IReadOnlyList<Domain.Events.SimEvent>> GetAllEventsAsync(int limit = 500) => Task.FromResult<IReadOnlyList<Domain.Events.SimEvent>>(_events.OrderByDescending(e => e.Ts).Take(limit).ToList());
}
