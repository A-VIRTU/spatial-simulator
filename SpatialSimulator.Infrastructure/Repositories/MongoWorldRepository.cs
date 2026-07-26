using MongoDB.Driver;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Infrastructure.Repositories;

public class MongoWorldRepository : IWorldRepository
{
    private readonly MongoDbContext _context;

    public MongoWorldRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<SpatialEntity?> GetAsync(string id)
    {
        return await _context.Entities.Find(e => e.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<SpatialEntity>> GetChildrenAsync(string parentId)
    {
        return await _context.Entities.Find(e => e.ParentId == parentId).ToListAsync();
    }

    public async Task<IReadOnlyList<SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null)
    {
        var filterBuilder = Builders<SpatialEntity>.Filter;
        var filter = filterBuilder.AnyEq(e => e.Ancestors, rootId);

        if (maxDepth.HasValue)
        {
            var root = await GetAsync(rootId);
            if (root != null)
            {
                filter &= filterBuilder.Lte(e => e.Depth, root.Depth + maxDepth.Value);
            }
        }

        return await _context.Entities.Find(filter).ToListAsync();
    }

    public async Task<IReadOnlyList<SpatialEntity>> GetAncestorsAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity == null || entity.Ancestors.Count == 0)
        {
            return Array.Empty<SpatialEntity>();
        }

        var filter = Builders<SpatialEntity>.Filter.In(e => e.Id, entity.Ancestors);
        var ancestorEntities = await _context.Entities.Find(filter).ToListAsync();

        return entity.Ancestors
            .Select(ancestorId => ancestorEntities.FirstOrDefault(a => a.Id == ancestorId))
            .OfType<SpatialEntity>()
            .ToList();
    }

    public async Task AddAsync(SpatialEntity entity)
    {
        await SetEntityHierarchyDetailsAsync(entity);
        await _context.Entities.InsertOneAsync(entity);
    }

    public async Task AddManyAsync(IEnumerable<SpatialEntity> entities)
    {
        var list = entities.ToList();
        foreach (var entity in list)
        {
            await SetEntityHierarchyDetailsAsync(entity);
        }
        if (list.Count > 0)
        {
            await _context.Entities.InsertManyAsync(list);
        }
    }

    public async Task ReplaceAsync(SpatialEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.Entities.ReplaceOneAsync(e => e.Id == entity.Id, entity);
    }

    public async Task ReparentAsync(string id, string newParentId)
    {
        var node = await GetAsync(id);
        if (node == null) return;

        var newParent = await GetAsync(newParentId);
        if (newParent == null) return;

        var newAncestors = newParent.Ancestors.Concat([newParentId]).ToList();

        node.ParentId = newParentId;
        node.Ancestors = newAncestors;
        node.Depth = newAncestors.Count;
        node.MaterializedPath = $"{newParent.MaterializedPath}/{node.Id}";
        node.UpdatedAt = DateTime.UtcNow;

        await ReplaceAsync(node);

        var descendants = await GetSubtreeAsync(id);
        if (descendants.Count == 0) return;

        var bulkOps = new List<WriteModel<SpatialEntity>>();
        foreach (var d in descendants)
        {
            var idx = d.Ancestors.IndexOf(id);
            if (idx >= 0)
            {
                var suffix = d.Ancestors.Skip(idx).ToList();
                var updatedAncestors = newAncestors.Concat(suffix).ToList();
                var newPath = $"{node.MaterializedPath}" + d.MaterializedPath.Substring(d.MaterializedPath.IndexOf($"/{id}") + id.Length + 1);

                var update = Builders<SpatialEntity>.Update
                    .Set(e => e.Ancestors, updatedAncestors)
                    .Set(e => e.Depth, updatedAncestors.Count)
                    .Set(e => e.MaterializedPath, newPath)
                    .Set(e => e.UpdatedAt, DateTime.UtcNow);

                bulkOps.Add(new UpdateOneModel<SpatialEntity>(Builders<SpatialEntity>.Filter.Eq(e => e.Id, d.Id), update));
            }
        }

        if (bulkOps.Count > 0)
        {
            await _context.Entities.BulkWriteAsync(bulkOps);
        }
    }

    public async Task DeleteAsync(string id)
    {
        var descendants = await GetSubtreeAsync(id);
        var idsToDelete = descendants.Select(d => d.Id).Append(id).ToList();
        await _context.Entities.DeleteManyAsync(Builders<SpatialEntity>.Filter.In(e => e.Id, idsToDelete));
    }

    private async Task SetEntityHierarchyDetailsAsync(SpatialEntity entity)
    {
        if (string.IsNullOrEmpty(entity.ParentId))
        {
            entity.Ancestors = [];
            entity.Depth = 0;
            entity.MaterializedPath = $"/{entity.Id}";
        }
        else
        {
            var parent = await GetAsync(entity.ParentId);
            if (parent != null)
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
}
