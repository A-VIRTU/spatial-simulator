using Microsoft.AspNetCore.Mvc;
using SpatialSimulator.Domain.Repositories;
using SpatialSimulator.Ingestion;

namespace SpatialSimulator.Api.Controllers;

/// <summary>
/// REST API ovladač pro inicializaci a seeder testovacích dat.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly IWorldRepository _worldRepository;
    private readonly IConnectivityRepository _connectivityRepository;

    /// <summary>
    /// Konstruktor seeder ovladače.
    /// </summary>
    public SeedController(IWorldRepository worldRepository, IConnectivityRepository connectivityRepository)
    {
        _worldRepository = worldRepository;
        _connectivityRepository = connectivityRepository;
    }

    /// <summary>
    /// Znovu naplní databázi testovacími geodaty Runářova.
    /// </summary>
    [HttpPost("runarov")]
    public async Task<IActionResult> SeedRunarov()
    {
        try
        {
            // Smaže staré sídlo
            await _worldRepository.DeleteAsync("settlement_runarov");

            string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            var seeder = new RealRunarovSeeder(_worldRepository, _connectivityRepository);
            await seeder.SeedRealRunarovAsync(dataDir);

            return Ok(new { message = "Runářov úspěšně pře-naplněn reálnými geodaty." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
