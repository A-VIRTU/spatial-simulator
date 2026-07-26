using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Agents;
using SpatialSimulator.Application.Services;

namespace SpatialSimulator.Api.Controllers;

/// <summary>
/// REST API ovladač pro spouštění kroků a akcí AI agentů.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AgentLoopDriver _agentLoopDriver;
    private readonly IAgentContextService _contextService;

    /// <summary>
    /// Konstruktor ovladače agentů.
    /// </summary>
    public AgentsController(AgentLoopDriver agentLoopDriver, IAgentContextService contextService)
    {
        _agentLoopDriver = agentLoopDriver;
        _contextService = contextService;
    }

    /// <summary>
    /// Provede jeden percepčně-akční krok simulace agenta.
    /// </summary>
    [HttpPost("{agentId}/step")]
    public async Task<IActionResult> Step(string agentId)
    {
        string response = await _agentLoopDriver.StepAsync(agentId);
        return Ok(new { agentId, response });
    }

    /// <summary>
    /// Zobrazí percepční kontext a vygenerovaný prompt pro zadaného agenta.
    /// </summary>
    [HttpGet("{agentId}/context")]
    public async Task<IActionResult> GetContext(string agentId)
    {
        var context = await _contextService.BuildAgentContextAsync(agentId);
        return Ok(context);
    }
}
