using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Creates event subscribers that translate agent status events into persisted project state.
/// </summary>
internal interface IEventSubscriberFactory
{
    /// <summary>
    /// Creates an event subscriber for a project's status event stream.
    /// </summary>
    /// <param name="projectId">Project identifier whose events will be consumed.</param>
    /// <param name="events">Observable status event stream produced by the review runtime.</param>
    /// <returns>The event subscriber that processes the stream sequentially.</returns>
    EventSubscriber Create(
        Guid projectId,
        IObservable<StatusEvent> events);
}
