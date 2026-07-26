using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain.Events;

namespace SpatialSimulator.Agents;

public interface ILlmClient
{
    Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt);
}

public class MockLlmClient : ILlmClient
{
    public Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt)
    {
        return Task.FromResult("Rozhlížím se kolem a pokračuji v plnění svého cíle.");
    }
}

public class AgentLoopDriver
{
    private readonly IAgentContextService _contextService;
    private readonly ISpatialMutatorService _mutatorService;
    private readonly IAgentMemoryService _memoryService;
    private readonly ILlmClient _llmClient;

    public AgentLoopDriver(
        IAgentContextService contextService,
        ISpatialMutatorService mutatorService,
        IAgentMemoryService memoryService,
        ILlmClient? llmClient = null)
    {
        _contextService = contextService;
        _mutatorService = mutatorService;
        _memoryService = memoryService;
        _llmClient = llmClient ?? new MockLlmClient();
    }

    public async Task<string> StepAsync(string agentId)
    {
        var ctx = await _contextService.BuildAgentContextAsync(agentId);
        string prompt = BuildAgentPrompt(ctx);

        string response = await _llmClient.GenerateResponseAsync(
            "Jsi AI agent v sémantickém prostorovém simulátoru.",
            prompt
        );

        // Record observation/thought event into GTU memory stream
        await _memoryService.RecordEventAsync(new SimEvent
        {
            Kind = "Observation",
            LocationId = ctx.CurrentLocation?.Id,
            Participants = [agentId],
            Text = $"{ctx.Agent.Name} přemýšlí: {response}",
            Importance = 3.0
        });

        return response;
    }

    private static string BuildAgentPrompt(AgentContext ctx)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Jsi agent: {ctx.Agent.Name}");
        if (!string.IsNullOrEmpty(ctx.Agent.Agent?.CurrentGoal))
        {
            sb.AppendLine($"Tvůj aktuální cíl: {ctx.Agent.Agent.CurrentGoal}");
        }

        sb.AppendLine($"\nAktuální poloha: {ctx.CurrentLocation?.Name ?? "Neznámé místo"}");
        if (!string.IsNullOrEmpty(ctx.CurrentLocation?.Semantic.Description))
        {
            sb.AppendLine($"Popis prostředí: {ctx.CurrentLocation.Semantic.Description}");
        }

        sb.AppendLine("\nCesta v hierarchii:");
        foreach (var ancestor in ctx.Ancestors)
        {
            sb.AppendLine($"- {ancestor.Type}: {ancestor.Name}");
        }

        sb.AppendLine("\nViditelné předměty a okolí:");
        foreach (var v in ctx.VisibleEntities)
        {
            sb.AppendLine($"- [{v.Type}] {v.Name} (ID: {v.Id})");
        }

        sb.AppendLine("\nDostupné východy/přechody:");
        foreach (var exit in ctx.AvailableExits)
        {
            sb.AppendLine($"- [Typ: {exit.Kind}] do {exit.ToId} (Stav: {exit.State})");
        }

        sb.AppendLine("\nRelevantní vzpomínky z minulosti:");
        foreach (var mem in ctx.RelevantMemories)
        {
            sb.AppendLine($"- [{mem.Event.Ts:HH:mm:ss}] {mem.Event.Text}");
        }

        sb.AppendLine("\nRozhodni se pro další krok.");
        return sb.ToString();
    }
}
