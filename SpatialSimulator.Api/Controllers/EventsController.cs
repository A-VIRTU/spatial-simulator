using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SpatialSimulator.Api.Hubs;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Api.Controllers;

/// <summary>
/// REST API ovladač pro přístup k událostem v časové ose GTU.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IHubContext<SimulationHub> _hubContext;

    /// <summary>
    /// Konstruktor ovladače událostí.
    /// </summary>
    public EventsController(IEventRepository eventRepository, IHubContext<SimulationHub> hubContext)
    {
        _eventRepository = eventRepository;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Vrátí seznam posledních událostí v simulaci.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SimEvent>>> GetEvents([FromQuery] int limit = 100)
    {
        var events = await _eventRepository.GetAllEventsAsync(limit);
        return Ok(events);
    }

    /// <summary>
    /// Přidá novou událost do simulace a pošle ji klientům přes SignalR.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] SimEvent simEvent)
    {
        await _eventRepository.AddAsync(simEvent);
        await _hubContext.Clients.All.SendAsync("EventRecorded", simEvent);
        return CreatedAtAction(nameof(GetEvents), new { id = simEvent.Id }, simEvent);
    }
}
