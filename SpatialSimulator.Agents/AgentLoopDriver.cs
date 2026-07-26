using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Agents;

/// <summary>
/// Ovladač percepčně-akční smyčky AI agentů.
/// Motivace: Řídí jeden krok simulace agenta — od percepce přes vyvolání pamětí až po vygenerování rozhodnutí a zápis do GTU.
/// </summary>
public class AgentLoopDriver
{
    private readonly IAgentContextService _contextService;
    private readonly ILlmClient _llmClient;
    private readonly IEventRepository _eventRepository;

    /// <summary>
    /// Konstruktor přijímající percepční službu, LLM klienta a repozitář událostí.
    /// </summary>
    public AgentLoopDriver(IAgentContextService contextService, ILlmClient llmClient, IEventRepository eventRepository)
    {
        _contextService = contextService;
        _llmClient = llmClient;
        _eventRepository = eventRepository;
    }

    /// <summary>
    /// Provede jeden percepčně-akční krok pro daného agenta.
    /// </summary>
    /// <param name="agentId">ID agenta, jehož krok se má provést.</param>
    /// <returns>Textové rozhodnutí / reakce agenta.</returns>
    public async Task<string> StepAsync(string agentId)
    {
        var context = await _contextService.BuildAgentContextAsync(agentId);

        string prompt = $"{context.PromptText}\n\nNyní navrhni další konkrétní krok nebo reakci v prostředí:";
        string llmResponse = await _llmClient.GenerateTextAsync(prompt);

        await _eventRepository.AddAsync(new SimEvent
        {
            Kind = "Reflection",
            LocationId = context.CurrentLocation?.Id,
            Participants = [agentId],
            Text = $"{context.AgentName} se rozhodl(a): {llmResponse}",
            Importance = 7.5,
            Ts = DateTime.UtcNow
        });

        return llmResponse;
    }
}
