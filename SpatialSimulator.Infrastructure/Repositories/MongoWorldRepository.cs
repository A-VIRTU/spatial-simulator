using MongoDB.Driver;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Infrastructure.Repositories;

/// <summary>
/// MongoDB implementace repozitáře pro správu stromu prostorových entit.
/// Motivace: Zajišťuje trvalé ukládání entit v MongoDB s podporou 2dsphere indexů a Array of Ancestors pro rychlé podstromové dotazy.
/// </summary>
public class MongoWorldRepository : IWorldRepository
{
    private readonly MongoDbContext _context;

    /// <summary>
    /// Konstruktor přijímající MongoDbContext.
    /// </summary>
    public MongoWorldRepository(MongoDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<SpatialEntity?> GetAsync(string id)
    {
        return await _context.Entities.Find(e => e.Id == id).FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SpatialEntity>> GetChildrenAsync(string parentId)
    {
        return await _context.Entities.Find(e => e.ParentId == parentId).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null)
    {
        var filter = Builders<SpatialEntity>.Filter.Or(
            Builders<SpatialEntity>.Filter.Eq(e => e.Id, rootId),
            Builders<SpatialEntity>.Filter.AnyEq(e => e.Ancestors, rootId)
        );

        return await _context.Entities.Find(filter).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SpatialEntity>> GetAncestorsAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity == null || entity.Ancestors.Count == 0) return [];

        var filter = Builders<SpatialEntity>.Filter.In(e => e.Id, entity.Ancestors);
        return await _context.Entities.Find(filter).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task AddAsync(SpatialEntity entity)
    {
        await SetHierarchyAsync(entity);
        await _context.Entities.InsertOneAsync(entity);
    }

    /// <inheritdoc/>
    public async Task AddManyAsync(IEnumerable<SpatialEntity> entities)
    {
        var list = entities.ToList();
        foreach (var e in list)
        {
            await SetHierarchyAsync(e);
        }
        if (list.Count > 0)
        {
            await _context.Entities.InsertManyAsync(list);
        }
    }

    /// <inheritdoc/>
    public async Task ReplaceAsync(SpatialEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.Entities.ReplaceOneAsync(e => e.Id == entity.Id, entity);
    }

    /// <inheritdoc/>
    public async Task ReparentAsync(string id, string newParentId)
    {
        var entity = await GetAsync(id);
        var newParent = await GetAsync(newParentId);
        if (entity == null || newParent == null) return;

        entity.ParentId = newParentId;
        entity.Ancestors = newParent.Ancestors.Concat([newParentId]).ToList();
        entity.Depth = entity.Ancestors.Count;
        entity.MaterializedPath = $"{newParent.MaterializedPath}/{entity.Id}";
        entity.UpdatedAt = DateTime.UtcNow;

        await ReplaceAsync(entity);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id)
    {
        var filter = Builders<SpatialEntity>.Filter.Or(
            Builders<SpatialEntity>.Filter.Eq(e => e.Id, id),
            Builders<SpatialEntity>.Filter.AnyEq(e => e.Ancestors, id)
        );
        await _context.Entities.DeleteManyAsync(filter);
    }

    private async Task SetHierarchyAsync(SpatialEntity entity)
    {
        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await GetAsync(entity.ParentId);
            if (parent != null)
            {
                entity.Ancestors = parent.Ancestors.Concat([parent.Id]).ToList();
                entity.Depth = entity.Ancestors.Count;
                entity.MaterializedPath = $"{parent.MaterializedPath}/{entity.Id}";
                return;
            }
        }

        entity.Ancestors = [];
        entity.Depth = 0;
        entity.MaterializedPath = $"/{entity.Id}";
    }
}
