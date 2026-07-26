using Microsoft.AspNetCore.SignalR;
using SpatialSimulator.Domain.Events;

namespace SpatialSimulator.Api.Hubs;

public interface ISimulationClient
{
    Task AgentMoved(string agentId, string fromNodeId, string toNodeId, DateTime simTime);
    Task EventRecorded(SimEvent simEvent);
    Task SimClockAdvanced(DateTime simTime);
}

public class SimulationHub : Hub<ISimulationClient>
{
}
