using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Observers;
using QuikGraph.Algorithms.ShortestPath;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

/// <summary>
/// Rozhraní grafové služby konektivity pro průchod a vyhledávání cest v simulaci.
/// </summary>
public interface IConnectivityGraphService
{
    /// <summary>
    /// Znovu načte hrany z repozitáře a sestaví graf propustnosti.
    /// </summary>
    Task ReloadGraphAsync();

    /// <summary>
    /// Vypočítá nejkratší dostupnou cestu mezi dvěma uzly pomocí Dijkstrova algoritmu.
    /// </summary>
    Task<List<string>> FindPathAsync(string startNodeId, string targetNodeId);
}

/// <summary>
/// Grafová služba konektivity využívající knihovnu QuikGraph pro výpočet nejkratších cest (Dijkstra).
/// Motivace: Zajišťuje realistickou navigaci agentů přes dveře, chodby, ulice a cesty.
/// </summary>
public class ConnectivityGraphService : IConnectivityGraphService
{
    private readonly IConnectivityRepository _connectivityRepository;
    private BidirectionalGraph<string, Edge<string>> _graph = new();
    private readonly Dictionary<Edge<string>, double> _costs = new();

    /// <summary>
    /// Konstruktor přijímající repozitář konektivity.
    /// </summary>
    public ConnectivityGraphService(IConnectivityRepository connectivityRepository)
    {
        _connectivityRepository = connectivityRepository;
    }

    /// <summary>
    /// Znovu načte hrany a sestaví vnitřní QuikGraph strukturu.
    /// </summary>
    public async Task ReloadGraphAsync()
    {
        var edges = await _connectivityRepository.GetAllEdgesAsync();
        var graph = new BidirectionalGraph<string, Edge<string>>();
        var costs = new Dictionary<Edge<string>, double>();

        foreach (var edge in edges)
        {
            if (edge.State == "Locked") continue; // Uzamčené dveře nelze použít pro automatický pathfinding

            graph.AddVertex(edge.FromId);
            graph.AddVertex(edge.ToId);

            var e1 = new Edge<string>(edge.FromId, edge.ToId);
            graph.AddEdge(e1);
            costs[e1] = edge.CostMeters;

            if (edge.Bidirectional)
            {
                var e2 = new Edge<string>(edge.ToId, edge.FromId);
                graph.AddEdge(e2);
                costs[e2] = edge.CostMeters;
            }
        }

        _graph = graph;
        _costs.Clear();
        foreach (var kv in costs) _costs[kv.Key] = kv.Value;
    }

    /// <summary>
    /// Vyhledá nejkratší trasu mezi uzly v metrech.
    /// </summary>
    public Task<List<string>> FindPathAsync(string startNodeId, string targetNodeId)
    {
        if (!_graph.ContainsVertex(startNodeId) || !_graph.ContainsVertex(targetNodeId))
            return Task.FromResult<List<string>>([]);

        Func<Edge<string>, double> edgeCost = e => _costs.TryGetValue(e, out double c) ? c : 1.0;
        var algo = new DijkstraShortestPathAlgorithm<string, Edge<string>>(_graph, edgeCost);

        var predecessorObserver = new VertexPredecessorPathRecorderObserver<string, Edge<string>>();
        using (predecessorObserver.Attach(algo))
        {
            algo.Compute(startNodeId);
        }

        if (predecessorObserver.VerticesPredecessors.TryGetPath(targetNodeId, out IEnumerable<Edge<string>>? pathEdges) && pathEdges != null)
        {
            var path = new List<string> { startNodeId };
            path.AddRange(pathEdges.Select(e => e.Target));
            return Task.FromResult(path);
        }

        return Task.FromResult<List<string>>([]);
    }
}
