using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

public record RetrievedMemory(SimEvent Event, double Score);

public interface IAgentMemoryService
{
    Task RecordEventAsync(SimEvent simEvent);
    Task<IReadOnlyList<RetrievedMemory>> RetrieveMemoriesAsync(string agentId, string queryText, int topK = 10);
}

public class AgentMemoryService : IAgentMemoryService
{
    private readonly IEventRepository _eventRepo;
    private const double AlphaRecency = 1.0;
    private const double BetaImportance = 1.0;
    private const double GammaRelevance = 1.0;

    public AgentMemoryService(IEventRepository eventRepo)
    {
        _eventRepo = eventRepo;
    }

    public async Task RecordEventAsync(SimEvent simEvent)
    {
        if (string.IsNullOrEmpty(simEvent.Id))
        {
            simEvent.Id = Guid.NewGuid().ToString("n");
        }
        await _eventRepo.AddAsync(simEvent);
    }

    public async Task<IReadOnlyList<RetrievedMemory>> RetrieveMemoriesAsync(string agentId, string queryText, int topK = 10)
    {
        var candidates = await _eventRepo.GetEventsForAgentAsync(agentId, limit: 200);
        if (candidates.Count == 0) return Array.Empty<RetrievedMemory>();

        var now = DateTime.UtcNow;
        var queryTokens = queryText.ToLowerInvariant().Split([' ', ',', '.', ';', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        var scored = candidates.Select(e =>
        {
            double recency = CalculateRecency(e.Ts, now);
            double importance = Math.Clamp(e.Importance / 10.0, 0.0, 1.0);
            double relevance = CalculateRelevance(e.Text, queryTokens);

            double totalScore = (AlphaRecency * recency) + (BetaImportance * importance) + (GammaRelevance * relevance);
            return new RetrievedMemory(e, totalScore);
        })
        .OrderByDescending(m => m.Score)
        .Take(topK)
        .ToList();

        return scored;
    }

    private static double CalculateRecency(DateTime eventTs, DateTime now)
    {
        double hoursPassed = Math.Max(0, (now - eventTs).TotalHours);
        return Math.Pow(0.99, hoursPassed);
    }

    private static double CalculateRelevance(string eventText, string[] queryTokens)
    {
        if (queryTokens.Length == 0 || string.IsNullOrWhiteSpace(eventText)) return 0.5;

        string textLower = eventText.ToLowerInvariant();
        int matches = queryTokens.Count(token => textLower.Contains(token));
        return (double)matches / queryTokens.Length;
    }
}
