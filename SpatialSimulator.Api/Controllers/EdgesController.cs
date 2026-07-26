using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Api.Controllers;

/// <summary>
/// REST API ovladač pro graf konektivity a vyhledávání tras.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EdgesController : ControllerBase
{
    private readonly IConnectivityRepository _connectivityRepository;
    private readonly IConnectivityGraphService _graphService;

    /// <summary>
    /// Konstruktor ovladače hran.
    /// </summary>
    public EdgesController(IConnectivityRepository connectivityRepository, IConnectivityGraphService graphService)
    {
        _connectivityRepository = connectivityRepository;
        _graphService = graphService;
    }

    /// <summary>
    /// Načte všechny dostupné hrany v databázi.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConnectivityEdge>>> GetAllEdges()
    {
        var edges = await _connectivityRepository.GetAllEdgesAsync();
        return Ok(edges);
    }

    /// <summary>
    /// Vypočítá nejkratší trasu mezi dvěma uzly pomocí Dijkstrova algoritmu.
    /// </summary>
    [HttpGet("path")]
    public async Task<ActionResult<IEnumerable<string>>> FindPath([FromQuery] string start, [FromQuery] string target)
    {
        var path = await _graphService.FindPathAsync(start, target);
        return Ok(path);
    }
}
