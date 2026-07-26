using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntitiesController : ControllerBase
{
    private readonly IWorldRepository _worldRepo;

    public EntitiesController(IWorldRepository worldRepo)
    {
        _worldRepo = worldRepo;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SpatialEntity>> GetEntity(string id)
    {
        var entity = await _worldRepo.GetAsync(id);
        if (entity == null) return NotFound();
        return Ok(entity);
    }

    [HttpGet("{id}/children")]
    public async Task<ActionResult<IReadOnlyList<SpatialEntity>>> GetChildren(string id)
    {
        var children = await _worldRepo.GetChildrenAsync(id);
        return Ok(children);
    }

    [HttpGet("{id}/subtree")]
    public async Task<ActionResult<IReadOnlyList<SpatialEntity>>> GetSubtree(string id, [FromQuery] int? maxDepth = null)
    {
        var subtree = await _worldRepo.GetSubtreeAsync(id, maxDepth);
        return Ok(subtree);
    }

    [HttpGet("{id}/ancestors")]
    public async Task<ActionResult<IReadOnlyList<SpatialEntity>>> GetAncestors(string id)
    {
        var ancestors = await _worldRepo.GetAncestorsAsync(id);
        return Ok(ancestors);
    }
}
