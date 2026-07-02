using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal interface IEventSubscriberFactory
{
    EventSubscriber Create(
        Guid projectId,
        IObservable<StatusEvent> events);
}
