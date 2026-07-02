using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal interface IEventHandler
{
    Task HandleAsync(StatusEvent agentEvent, CancellationToken cancellationToken);
}
