using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Handles status events emitted by the review runtime.
/// </summary>
internal interface IEventHandler
{
    /// <summary>
    /// Handles a single status event.
    /// </summary>
    /// <param name="agentEvent">Status event to handle.</param>
    /// <param name="cancellationToken">Token that cancels handling.</param>
    Task HandleAsync(StatusEvent agentEvent, CancellationToken cancellationToken);
}
