using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Application.Services;

/// <summary>
/// Záznam vyhledané paměti agenta spojené se skóre z paměťového algoritmu.
/// </summary>
public class ScoredMemory
{
    /// <summary>
    /// Záznam primární simulované události z GTU logu.
    /// </summary>
    public SimEvent Event { get; set; } = new();

    /// <summary>
    /// Celkové skóre paměti vypočítané dle vzorce Park et al. (2023).
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Časová složka čerstvosti vzpomínky (Recency).
    /// </summary>
    public double RecencyScore { get; set; }

    /// <summary>
    /// Složka důležitosti vzpomínky (Importance).
    /// </summary>
    public double ImportanceScore { get; set; }

    /// <summary>
    /// Složka sémantické relevantnosti k dotazu (Relevance).
    /// </summary>
    public double RelevanceScore { get; set; }
}

/// <summary>
/// Rozhraní paměťové služby agentů.
/// </summary>
public interface IAgentMemoryService
{
    /// <summary>
    /// Vyhledá a seřadí paměťové záznamy agenta odpovídající zadanému dotazu.
    /// </summary>
    Task<List<ScoredMemory>> RetrieveMemoriesAsync(string agentId, string query, int topK = 5);
}

/// <summary>
/// Paměťová služba agentů implementující Stanford Generative Agents paměťový vzorec (Park et al. 2023).
/// Skóre paměti = alpha_recency * Recency + alpha_importance * Importance + alpha_relevance * Relevance.
/// Motivace: Zajišťuje realistické vyvolávání vzpomínek podle jejich čerstvosti, důležitosti a vztahu k aktuální situaci.
/// </summary>
public class AgentMemoryService : IAgentMemoryService
{
    private readonly IEventRepository _eventRepository;

    /// <summary>
    /// Konstruktor přijímající repozitář událostí.
    /// </summary>
    public AgentMemoryService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    /// <summary>
    /// Vyhledá a ohodnotí paměťové záznamy z paměťového streamu agenta.
    /// </summary>
    public async Task<List<ScoredMemory>> RetrieveMemoriesAsync(string agentId, string query, int topK = 5)
    {
        var events = await _eventRepository.GetEventsForAgentAsync(agentId, 200);
        if (events.Count == 0) return [];

        double alphaRecency = 1.0;
        double alphaImportance = 1.0;
        double alphaRelevance = 1.0;

        DateTime now = DateTime.UtcNow;
        var scoredList = new List<ScoredMemory>();

        foreach (var evt in events)
        {
            // 1. Recency score: exponenta z času od události (v hodinách)
            double hoursAgo = (now - evt.Ts).TotalHours;
            double recency = Math.Exp(-0.99 * hoursAgo);

            // 2. Importance score: normalizovaná hodnota na škále 0..1
            double importance = evt.Importance / 10.0;

            // 3. Relevance score: jednoduchá textová shoda nebo kosinový odhad
            double relevance = 0.1;
            if (!string.IsNullOrWhiteSpace(query) && evt.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                relevance = 1.0;
            }

            double totalScore = (alphaRecency * recency) + (alphaImportance * importance) + (alphaRelevance * relevance);

            scoredList.Add(new ScoredMemory
            {
                Event = evt,
                Score = totalScore,
                RecencyScore = recency,
                ImportanceScore = importance,
                RelevanceScore = relevance
            });
        }

        return scoredList.OrderByDescending(m => m.Score).Take(topK).ToList();
    }
}
