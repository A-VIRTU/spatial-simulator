using SpatialSimulator.Agents;
using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;
using SpatialSimulator.Ingestion;
using Xunit;

namespace SpatialSimulator.Tests;

public class InMemoryWorldRepository : IWorldRepository
{
    private readonly Dictionary<string, SpatialEntity> _store = new();

    public Task<SpatialEntity?> GetAsync(string id)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<SpatialEntity>> GetChildrenAsync(string parentId)
    {
        var results = _store.Values.Where(e => e.ParentId == parentId).ToList();
        return Task.FromResult<IReadOnlyList<SpatialEntity>>(results);
    }

    public Task<IReadOnlyList<SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null)
    {
        var results = _store.Values.Where(e => e.Ancestors.Contains(rootId) || e.Id == rootId).ToList();
        return Task.FromResult<IReadOnlyList<SpatialEntity>>(results);
    }

    public Task<IReadOnlyList<SpatialEntity>> GetAncestorsAsync(string id)
    {
        if (!_store.TryGetValue(id, out var entity)) return Task.FromResult<IReadOnlyList<SpatialEntity>>([]);
        var list = entity.Ancestors.Select(aId => _store.TryGetValue(aId, out var a) ? a : null!).Where(a => a != null).ToList();
        return Task.FromResult<IReadOnlyList<SpatialEntity>>(list);
    }

    public Task AddAsync(SpatialEntity entity)
    {
        SetHierarchy(entity);
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task AddManyAsync(IEnumerable<SpatialEntity> entities)
    {
        foreach (var entity in entities) AddAsync(entity);
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(SpatialEntity entity)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task ReparentAsync(string id, string newParentId)
    {
        if (!_store.TryGetValue(id, out var node)) return Task.CompletedTask;
        if (!_store.TryGetValue(newParentId, out var newParent)) return Task.CompletedTask;

        node.ParentId = newParentId;
        node.Ancestors = newParent.Ancestors.Concat([newParentId]).ToList();
        node.Depth = node.Ancestors.Count;
        node.MaterializedPath = $"{newParent.MaterializedPath}/{node.Id}";
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }

    private void SetHierarchy(SpatialEntity entity)
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

public class InMemoryConnectivityRepository : IConnectivityRepository
{
    private readonly List<ConnectivityEdge> _edges = [];

    public Task<ConnectivityEdge?> GetAsync(string id) => Task.FromResult(_edges.FirstOrDefault(e => e.Id == id));
    public Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId) =>
        Task.FromResult<IReadOnlyList<ConnectivityEdge>>(_edges.Where(e => e.FromId == nodeId || (e.ToId == nodeId && e.Bidirectional)).ToList());
    public Task<IReadOnlyList<ConnectivityEdge>> GetAllEdgesAsync() => Task.FromResult<IReadOnlyList<ConnectivityEdge>>(_edges);
    public Task AddAsync(ConnectivityEdge edge) { _edges.Add(edge); return Task.CompletedTask; }
    public Task AddManyAsync(IEnumerable<ConnectivityEdge> edges) { _edges.AddRange(edges); return Task.CompletedTask; }
    public Task UpdateStateAsync(string edgeId, string state)
    {
        var edge = _edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge != null) edge.State = state;
        return Task.CompletedTask;
    }
}

public class InMemoryEventRepository : IEventRepository
{
    private readonly List<SimEvent> _events = [];
    public Task AddAsync(SimEvent simEvent) { _events.Add(simEvent); return Task.CompletedTask; }
    public Task<IReadOnlyList<SimEvent>> GetEventsForAgentAsync(string agentId, int limit = 100) =>
        Task.FromResult<IReadOnlyList<SimEvent>>(_events.Where(e => e.Participants.Contains(agentId)).OrderByDescending(e => e.Ts).Take(limit).ToList());
    public Task<IReadOnlyList<SimEvent>> GetEventsForLocationAsync(string locationId, int limit = 100) =>
        Task.FromResult<IReadOnlyList<SimEvent>>(_events.Where(e => e.LocationId == locationId).OrderByDescending(e => e.Ts).Take(limit).ToList());
    public Task<IReadOnlyList<SimEvent>> GetAllEventsAsync(int limit = 500) =>
        Task.FromResult<IReadOnlyList<SimEvent>>(_events.OrderByDescending(e => e.Ts).Take(limit).ToList());
}

public class SpatialSimulatorTests
{
    [Fact]
    public async Task TestContainmentHierarchyAndReparenting()
    {
        var worldRepo = new InMemoryWorldRepository();

        var house = new SpatialEntity { Id = "house_1", Type = SpatialEntityTypes.Building, Name = "Dům" };
        await worldRepo.AddAsync(house);

        var room = new SpatialEntity { Id = "room_1", Type = SpatialEntityTypes.Room, Name = "Kuchyň", ParentId = house.Id };
        await worldRepo.AddAsync(room);

        var agent = new SpatialEntity { Id = "agent_1", Type = SpatialEntityTypes.Agent, Name = "Jana", ParentId = room.Id };
        await worldRepo.AddAsync(agent);

        Assert.Equal(2, agent.Depth);
        Assert.Equal(["house_1", "room_1"], agent.Ancestors);
        Assert.Equal("/house_1/room_1/agent_1", agent.MaterializedPath);

        var room2 = new SpatialEntity { Id = "room_2", Type = SpatialEntityTypes.Room, Name = "Obývák", ParentId = house.Id };
        await worldRepo.AddAsync(room2);

        await worldRepo.ReparentAsync(agent.Id, room2.Id);

        var updatedAgent = await worldRepo.GetAsync(agent.Id);
        Assert.NotNull(updatedAgent);
        Assert.Equal(["house_1", "room_2"], updatedAgent.Ancestors);
    }

    [Fact]
    public async Task TestGraphPathfinding()
    {
        var graphRepo = new InMemoryConnectivityRepository();
        await graphRepo.AddAsync(new ConnectivityEdge { Id = "e1", FromId = "A", ToId = "B", CostMeters = 10.0 });
        await graphRepo.AddAsync(new ConnectivityEdge { Id = "e2", FromId = "B", ToId = "C", CostMeters = 5.0 });

        var graphService = new ConnectivityGraphService(graphRepo);
        await graphService.ReloadGraphAsync();

        var path = await graphService.FindPathAsync("A", "C");
        Assert.Equal(["A", "B", "C"], path);
    }

    [Fact]
    public async Task TestMemoryRetrievalScoring()
    {
        var eventRepo = new InMemoryEventRepository();
        await eventRepo.AddAsync(new SimEvent
        {
            Id = "ev1",
            Participants = ["agent_1"],
            Text = "Jana našla v kapse krabičku sirek",
            Importance = 8.0,
            Ts = DateTime.UtcNow
        });

        var memoryService = new AgentMemoryService(eventRepo);
        var memories = await memoryService.RetrieveMemoriesAsync("agent_1", "sirky", topK: 5);

        Assert.Single(memories);
        Assert.Equal("ev1", memories[0].Event.Id);
        Assert.True(memories[0].Score > 1.0);
    }

    [Fact]
    public async Task TestWorldGenerationServiceRuleExpansion()
    {
        var worldRepo = new InMemoryWorldRepository();
        var connRepo = new InMemoryConnectivityRepository();
        var genService = new WorldGenerationService(worldRepo, connRepo);

        var building = new SpatialEntity
        {
            Id = "building_test",
            Type = SpatialEntityTypes.Building,
            Name = "Testovací dům",
            Semantic = new SemanticComponent { Attributes = new Dictionary<string, object> { { "floors", 2 } } }
        };
        await worldRepo.AddAsync(building);

        await genService.EnsureChildrenAsync(building.Id);

        var floors = await worldRepo.GetChildrenAsync(building.Id);
        Assert.Equal(2, floors.Count);
        Assert.Equal(GenerationState.Outlined, building.Generation.State);
    }

    [Fact]
    public async Task TestSpatialMutatorServiceItemMove()
    {
        var worldRepo = new InMemoryWorldRepository();
        var connRepo = new InMemoryConnectivityRepository();
        var eventRepo = new InMemoryEventRepository();
        var mutator = new SpatialMutatorService(worldRepo, connRepo, eventRepo);

        var room = new SpatialEntity { Id = "room_1", Type = SpatialEntityTypes.Room, Name = "Kuchyň" };
        var agent = new SpatialEntity { Id = "agent_1", Type = SpatialEntityTypes.Agent, Name = "Jana", ParentId = room.Id };
        var item = new SpatialEntity { Id = "item_sirky", Type = SpatialEntityTypes.Item, Name = "Sirky", ParentId = room.Id };

        await worldRepo.AddAsync(room);
        await worldRepo.AddAsync(agent);
        await worldRepo.AddAsync(item);

        bool taken = await mutator.TakeItemAsync("agent_1", "item_sirky");
        Assert.True(taken);

        var updatedItem = await worldRepo.GetAsync("item_sirky");
        Assert.Equal("agent_1", updatedItem?.ParentId);

        var events = await eventRepo.GetAllEventsAsync();
        Assert.Single(events);
        Assert.Contains("sebral předmět", events[0].Text);
    }

    [Fact]
    public void TestSJtskToWgs84CoordinateConversion()
    {
        var (lat, lon) = SpatialProjection.SJtskToWgs84(564500, 1052000);
        Assert.InRange(lat, 48.0, 51.0);
        Assert.InRange(lon, 12.0, 19.0);
    }

    [Fact]
    public void TestRuianVfrXmlParser()
    {
        string vfrXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<vfr:VFR xmlns:vfr=""http://www.cuzk.cz/ruian/vfr/v1"" xmlns:gml=""http://www.opengis.net/gml/3.2"">
    <vfr:StavebniObjekt>
        <vfr:Kod>12345</vfr:Kod>
        <vfr:CislaDomovni><vfr:Cislo>23</vfr:Cislo></vfr:CislaDomovni>
        <vfr:PocetPodlazi>1</vfr:PocetPodlazi>
        <gml:posList>564500 1052000</gml:posList>
    </vfr:StavebniObjekt>
</vfr:VFR>";

        var parser = new RuianVfrParser();
        var entities = parser.ParseVfrXml(vfrXml);

        Assert.Single(entities);
        Assert.Equal("Čp. 23", entities[0].Name);
        Assert.Equal("building_ruian_12345", entities[0].Id);
        Assert.NotNull(entities[0].Spatial?.GlobalAnchor);
    }

    [Fact]
    public void TestOsmOverpassJsonParser()
    {
        string osmJson = @"{
            ""elements"": [
                { ""type"": ""node"", ""id"": 991, ""lat"": 49.543, ""lon"": 16.896, ""tags"": { ""amenity"": ""chapel"", ""name"": ""Kaplička"" } },
                { ""type"": ""node"", ""id"": 992, ""lat"": 49.544, ""lon"": 16.897 },
                { ""type"": ""way"", ""id"": 881, ""nodes"": [991, 992], ""tags"": { ""highway"": ""residential"" }, ""geometry"": [ { ""lat"": 49.543, ""lon"": 16.896 }, { ""lat"": 49.544, ""lon"": 16.897 } ] }
            ]
        }";

        var parser = new OsmOverpassParser();
        var result = parser.ParseOverpassJson(osmJson);

        var chapel = Assert.Single(result.Entities, e => e.Name == "Kaplička");
        Assert.NotNull(chapel);
        Assert.NotEmpty(result.Edges);
        Assert.Equal("Road", result.Edges[0].Kind);
    }
}
