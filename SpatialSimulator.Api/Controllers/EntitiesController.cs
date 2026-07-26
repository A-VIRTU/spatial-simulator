using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Api.Controllers;

/// <summary>
/// REST API ovladač pro správy a inspekce prostorových entit.
/// Motivace: Poskytuje koncové body pro čtení entit, načítání stromových větví a přesuny v hierarchii.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EntitiesController : ControllerBase
{
    private readonly IWorldRepository _worldRepository;

    /// <summary>
    /// Konstruktor přijímající repozitář světa.
    /// </summary>
    public EntitiesController(IWorldRepository worldRepository)
    {
        _worldRepository = worldRepository;
    }

    /// <summary>
    /// Načte detail jedné prostorové entity podle ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SpatialEntity>> GetEntity(string id)
    {
        var entity = await _worldRepository.GetAsync(id);
        if (entity == null) return NotFound();
        return Ok(entity);
    }

    /// <summary>
    /// Načte přímé dětské uzly dané entity.
    /// </summary>
    [HttpGet("{id}/children")]
    public async Task<ActionResult<IEnumerable<SpatialEntity>>> GetChildren(string id)
    {
        var children = await _worldRepository.GetChildrenAsync(id);
        return Ok(children);
    }

    /// <summary>
    /// Načte kompletní podstrom entit spadajících pod zadaný kořenový uzel.
    /// </summary>
    [HttpGet("{id}/subtree")]
    public async Task<ActionResult<IEnumerable<SpatialEntity>>> GetSubtree(string id)
    {
        var subtree = await _worldRepository.GetSubtreeAsync(id);
        return Ok(subtree);
    }

    /// <summary>
    /// Přesune entitu pod nového rodiče (Reparenting).
    /// </summary>
    [HttpPost("{id}/reparent")]
    public async Task<IActionResult> Reparent(string id, [FromQuery] string newParentId)
    {
        await _worldRepository.ReparentAsync(id, newParentId);
        return NoContent();
    }
}
