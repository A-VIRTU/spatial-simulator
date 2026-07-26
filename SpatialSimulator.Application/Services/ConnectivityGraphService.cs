using QuikGraph;
using QuikGraph.Algorithms;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

public interface IConnectivityGraphService
{
    Task ReloadGraphAsync();
    Task<IReadOnlyList<string>> FindPathAsync(string fromId, string toId);
    Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId);
}

public class EdgeWrapper : IEdge<string>
{
    public string Source { get; }
    public string Target { get; }
    public ConnectivityEdge Edge { get; }

    public EdgeWrapper(string source, string target, ConnectivityEdge edge)
    {
        Source = source;
        Target = target;
        Edge = edge;
    }
}

public class ConnectivityGraphService : IConnectivityGraphService
{
    private readonly IConnectivityRepository _connectivityRepo;
    private AdjacencyGraph<string, EdgeWrapper> _graph = new();
    private readonly Dictionary<string, List<ConnectivityEdge>> _edgeLookup = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ConnectivityGraphService(IConnectivityRepository connectivityRepo)
    {
        _connectivityRepo = connectivityRepo;
    }

    public async Task ReloadGraphAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var edges = await _connectivityRepo.GetAllEdgesAsync();
            var newGraph = new AdjacencyGraph<string, EdgeWrapper>();
            _edgeLookup.Clear();

            foreach (var edge in edges)
            {
                if (edge.State == "Locked") continue;

                if (!_edgeLookup.ContainsKey(edge.FromId)) _edgeLookup[edge.FromId] = [];
                _edgeLookup[edge.FromId].Add(edge);

                newGraph.AddVertex(edge.FromId);
                newGraph.AddVertex(edge.ToId);
                newGraph.AddEdge(new EdgeWrapper(edge.FromId, edge.ToId, edge));

                if (edge.Bidirectional)
                {
                    if (!_edgeLookup.ContainsKey(edge.ToId)) _edgeLookup[edge.ToId] = [];
                    _edgeLookup[edge.ToId].Add(edge);
                    newGraph.AddEdge(new EdgeWrapper(edge.ToId, edge.FromId, edge));
                }
            }

            _graph = newGraph;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ConnectivityEdge>> GetEdgesFromAsync(string nodeId)
    {
        return await _connectivityRepo.GetEdgesFromAsync(nodeId);
    }

    public async Task<IReadOnlyList<string>> FindPathAsync(string fromId, string toId)
    {
        if (fromId == toId) return [fromId];

        await _lock.WaitAsync();
        try
        {
            if (!_graph.ContainsVertex(fromId) || !_graph.ContainsVertex(toId))
            {
                return [];
            }

            var edgeCosts = new Func<EdgeWrapper, double>(e => e.Edge.CostMeters);
            var tryGetPaths = _graph.ShortestPathsDijkstra(edgeCosts, fromId);

            if (tryGetPaths(toId, out var path))
            {
                var result = new List<string> { fromId };
                foreach (var edge in path)
                {
                    result.Add(edge.Target);
                }
                return result;
            }

            return [];
        }
        finally
        {
            _lock.Release();
        }
    }
}
