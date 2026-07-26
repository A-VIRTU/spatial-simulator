using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EdgesController : ControllerBase
{
    private readonly IConnectivityRepository _connectivityRepo;
    private readonly IConnectivityGraphService _graphService;

    public EdgesController(IConnectivityRepository connectivityRepo, IConnectivityGraphService graphService)
    {
        _connectivityRepo = connectivityRepo;
        _graphService = graphService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectivityEdge>>> GetEdges([FromQuery] string? fromId = null)
    {
        if (!string.IsNullOrEmpty(fromId))
        {
            var edges = await _connectivityRepo.GetEdgesFromAsync(fromId);
            return Ok(edges);
        }

        var allEdges = await _connectivityRepo.GetAllEdgesAsync();
        return Ok(allEdges);
    }

    [HttpGet("path")]
    public async Task<ActionResult<IReadOnlyList<string>>> FindPath([FromQuery] string fromId, [FromQuery] string toId)
    {
        var path = await _graphService.FindPathAsync(fromId, toId);
        return Ok(path);
    }
}

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventRepository _eventRepo;

    public EventsController(IEventRepository eventRepo)
    {
        _eventRepo = eventRepo;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SimEvent>>> GetEvents(
        [FromQuery] string? locationId = null,
        [FromQuery] string? participantId = null,
        [FromQuery] int limit = 100)
    {
        if (!string.IsNullOrEmpty(locationId))
        {
            var locEvents = await _eventRepo.GetEventsForLocationAsync(locationId, limit);
            return Ok(locEvents);
        }

        if (!string.IsNullOrEmpty(participantId))
        {
            var agentEvents = await _eventRepo.GetEventsForAgentAsync(participantId, limit);
            return Ok(agentEvents);
        }

        var allEvents = await _eventRepo.GetAllEventsAsync(limit);
        return Ok(allEvents);
    }
}
