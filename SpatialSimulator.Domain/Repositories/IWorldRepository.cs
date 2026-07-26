using SpatialSimulator.Domain.Entities;

namespace SpatialSimulator.Domain.Repositories;

public interface IWorldRepository
{
    Task<SpatialEntity?> GetAsync(string id);
    Task<IReadOnlyList<SpatialEntity>> GetChildrenAsync(string parentId);
    Task<IReadOnlyList<SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null);
    Task<IReadOnlyList<SpatialEntity>> GetAncestorsAsync(string id);
    Task AddAsync(SpatialEntity entity);
    Task AddManyAsync(IEnumerable<SpatialEntity> entities);
    Task ReplaceAsync(SpatialEntity entity);
    Task ReparentAsync(string id, string newParentId);
    Task DeleteAsync(string id);
}
