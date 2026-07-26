using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Agents;
using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Repositories;
using SpatialSimulator.Ingestion;

namespace SpatialSimulator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IWorldRepository _worldRepo;
    private readonly IAgentContextService _contextService;
    private readonly AgentLoopDriver _agentDriver;

    public AgentsController(
        IWorldRepository worldRepo,
        IAgentContextService contextService,
        AgentLoopDriver agentDriver)
    {
        _worldRepo = worldRepo;
        _contextService = contextService;
        _agentDriver = agentDriver;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SpatialEntity>>> GetAgents()
    {
        var settlement = await _worldRepo.GetAsync("settlement_runarov");
        if (settlement == null) return Ok(Array.Empty<SpatialEntity>());

        var allSubtree = await _worldRepo.GetSubtreeAsync(settlement.Id);
        var agents = allSubtree.Where(e => e.Type == SpatialEntityTypes.Agent).ToList();
        return Ok(agents);
    }

    [HttpGet("{id}/context")]
    public async Task<ActionResult<AgentContext>> GetAgentContext(string id)
    {
        var ctx = await _contextService.BuildAgentContextAsync(id);
        return Ok(ctx);
    }

    [HttpPost("{id}/step")]
    public async Task<ActionResult<string>> StepAgent(string id)
    {
        string result = await _agentDriver.StepAsync(id);
        return Ok(new { Response = result });
    }
}

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly IWorldRepository _worldRepo;
    private readonly IConnectivityRepository _connectivityRepo;
    private readonly IConnectivityGraphService _graphService;

    public SeedController(
        IWorldRepository worldRepo,
        IConnectivityRepository connectivityRepo,
        IConnectivityGraphService graphService)
    {
        _worldRepo = worldRepo;
        _connectivityRepo = connectivityRepo;
        _graphService = graphService;
    }

    [HttpPost("runarov")]
    public async Task<ActionResult> SeedRunarov()
    {
        var seeder = new RunarovSeeder(_worldRepo, _connectivityRepo);
        await seeder.SeedAsync();
        await _graphService.ReloadGraphAsync();
        return Ok(new { Message = "Runářov dataset seeded successfully.", BuildingCount = 110 });
    }
}
