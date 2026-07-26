using Microsoft.AspNetCore.SignalR;

namespace SpatialSimulator.Api.Hubs;

/// <summary>
/// SignalR rozhraní pro vícenásobné připojení webových klientoů v reálném čase.
/// Motivace: Zajišťuje živé vysílání událostí z GTU streamu do administračního rozhraní.
/// </summary>
public class SimulationHub : Hub
{
    /// <summary>
    /// Umožňuje klientovi přihlásit se k odebrání živých aktualizací ze simulace.
    /// </summary>
    public async Task JoinSimulationGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
