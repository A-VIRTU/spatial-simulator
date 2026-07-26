using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Infrastructure.Repositories;

/// <summary>
/// In-Memory repozitář světa pro testovací a offline účely.
/// </summary>
public class InMemoryWorldRepository : IWorldRepository
{
    private readonly Dictionary<string, SpatialEntity> _store = new();
    public Task<SpatialEntity?> GetAsync(string id) { _store.TryGetValue(id, out var e); return Task.FromResult(e); }
    public Task<IReadOnlyList<SpatialEntity>> GetChildrenAsync(string parentId) => Task.FromResult<IReadOnlyList<SpatialEntity>>(_store.Values.Where(e => e.ParentId == parentId).ToList());
    public Task<IReadOnlyList<SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null) => Task.FromResult<IReadOnlyList<SpatialEntity>>(_store.Values.Where(e => e.Ancestors.Contains(rootId) || e.Id == rootId).ToList());
    public Task<IReadOnlyList<SpatialEntity>> GetAncestorsAsync(string id)
    {
        if (!_store.TryGetValue(id, out var entity)) return Task.FromResult<IReadOnlyList<SpatialEntity>>([]);
        var list = entity.Ancestors.Select(aId => _store.TryGetValue(aId, out var a) ? a : null!).Where(a => a != null).ToList();
        return Task.FromResult<IReadOnlyList<SpatialEntity>>(list);
    }
    public Task AddAsync(SpatialEntity entity) { SetHierarchy(entity); _store[entity.Id] = entity; return Task.CompletedTask; }
    public Task AddManyAsync(IEnumerable<SpatialEntity> entities) { foreach (var e in entities) AddAsync(e); return Task.CompletedTask; }
    public Task ReplaceAsync(SpatialEntity entity) { _store[entity.Id] = entity; return Task.CompletedTask; }
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

/// <summary>
/// In-Memory repozitář konektivity pro testovací a offline účely.
/// </summary>
public class InMemoryConnectivityRepository : IConnectivityRepository
{
    private readonly List<ConnectivityEdge> _edges = [];
    public Task<ConnectivityEdge?> GetAsync(string id) => Task.FromResult(_edges.FirstOrDefault(e => e.Id == id));
    public Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId) => Task.FromResult<IReadOnlyList<ConnectivityEdge>>(_edges.Where(e => e.FromId == nodeId || (e.ToId == nodeId && e.Bidirectional)).ToList());
    public Task<IReadOnlyList<ConnectivityEdge>> GetAllEdgesAsync() => Task.FromResult<IReadOnlyList<ConnectivityEdge>>(_edges);
    public Task AddAsync(ConnectivityEdge edge) { _edges.Add(edge); return Task.CompletedTask; }
    public Task AddManyAsync(IEnumerable<ConnectivityEdge> edges) { _edges.AddRange(edges); return Task.CompletedTask; }
    public Task UpdateStateAsync(string edgeId, string state) { var e = _edges.FirstOrDefault(x => x.Id == edgeId); if (e != null) e.State = state; return Task.CompletedTask; }
}
