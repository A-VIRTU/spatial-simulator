using SpatialSimulator.Domain.Entities;

namespace SpatialSimulator.Domain.Repositories;

/// <summary>
/// Repozitář pro správu a dotazování nad stromem prostorových entit.
/// Motivace: Poskytuje abstraktní rozhraní pro operace nad stromem obsahování v databázi i in-memory testech.
/// </summary>
public interface IWorldRepository
{
    /// <summary>
    /// Načte jednu prostorovou entitu podle jejího unikátního ID.
    /// </summary>
    Task<SpatialEntity?> GetAsync(string id);

    /// <summary>
    /// Načte všechny přímé dětské uzly dané rodičovské entity.
    /// </summary>
    Task<IReadOnlyList<SpatialEntity>> GetChildrenAsync(string parentId);

    /// <summary>
    /// Načte kompletní podstrom uzlů pod zadaným kořenovým uzlem.
    /// </summary>
    Task<IReadOnlyList<SpatialEntity>> GetSubtreeAsync(string rootId, int? maxDepth = null);

    /// <summary>
    /// Načte seznam všech předků daného uzlu v pořadí od kořene k rodiči.
    /// </summary>
    Task<IReadOnlyList<SpatialEntity>> GetAncestorsAsync(string id);

    /// <summary>
    /// Přidá novou prostorovou entitu do repozitáře.
    /// </summary>
    Task AddAsync(SpatialEntity entity);

    /// <summary>
    /// Hromadně přidá více prostorových entit.
    /// </summary>
    Task AddManyAsync(IEnumerable<SpatialEntity> entities);

    /// <summary>
    /// Nahradí/aktualizuje stávající entitu v repozitáři.
    /// </summary>
    Task ReplaceAsync(SpatialEntity entity);

    /// <summary>
    /// Přesune entitu pod nového rodiče a přepočítá Ancestors a MaterializedPath pro celý podstrom.
    /// </summary>
    Task ReparentAsync(string id, string newParentId);

    /// <summary>
    /// Smaže entitu a všechny její potomky ze stromu.
    /// </summary>
    Task DeleteAsync(string id);
}
