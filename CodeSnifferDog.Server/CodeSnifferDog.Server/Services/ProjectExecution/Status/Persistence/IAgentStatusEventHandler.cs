using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal interface IAgentStatusEventHandler
{
    Task HandleAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken);
}
