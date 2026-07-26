using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;

namespace SpatialSimulator.Domain.Repositories;

/// <summary>
/// Repozitář pro správu hran konektivitního grafu přechodů.
/// Motivace: Zprostředkovává přístup ke grafu propustnosti pro pathfinding.
/// </summary>
public interface IConnectivityRepository
{
    /// <summary>
    /// Načte hrana podle jejího ID.
    /// </summary>
    Task<ConnectivityEdge?> GetAsync(string id);

    /// <summary>
    /// Načte všechny dostupné hrany vedoucí ze zadaného uzlu.
    /// </summary>
    Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId);

    /// <summary>
    /// Načte všechny hrany grafu konektivity v databázi.
    /// </summary>
    Task<IReadOnlyList<ConnectivityEdge>> GetAllEdgesAsync();

    /// <summary>
    /// Přidá novou hranu konektivity.
    /// </summary>
    Task AddAsync(ConnectivityEdge edge);

    /// <summary>
    /// Hromadně přidá více hran konektivity.
    /// </summary>
    Task AddManyAsync(IEnumerable<ConnectivityEdge> edges);

    /// <summary>
    /// Aktualizuje stav hrany ("Open", "Closed", "Locked").
    /// </summary>
    Task UpdateStateAsync(string edgeId, string state);
}

/// <summary>
/// Repozitář pro ukládání a dotazování nad globální časovou osou událostí (GTU).
/// Motivace: Zajišťuje trvalé ukládání událostí a vyhledávání pamětí agentů.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Přidá novou událost do GTU logu.
    /// </summary>
    Task AddAsync(SimEvent simEvent);

    /// <summary>
    /// Načte události týkající se zadaného agenta.
    /// </summary>
    Task<IReadOnlyList<SimEvent>> GetEventsForAgentAsync(string agentId, int limit = 100);

    /// <summary>
    /// Načte události odehrané v zadané lokaci.
    /// </summary>
    Task<IReadOnlyList<SimEvent>> GetEventsForLocationAsync(string locationId, int limit = 100);

    /// <summary>
    /// Načte chronologický výpis všech událostí v simulaci.
    /// </summary>
    Task<IReadOnlyList<SimEvent>> GetAllEventsAsync(int limit = 500);
}
