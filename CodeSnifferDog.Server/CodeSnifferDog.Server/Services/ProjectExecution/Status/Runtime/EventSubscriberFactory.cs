using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal sealed class EventSubscriberFactory(IRuntimeFactory runtimeFactory)
    : IEventSubscriberFactory
{
    private readonly IRuntimeFactory _runtimeFactory = runtimeFactory;

    public EventSubscriber Create(
        Guid projectId,
        IObservable<StatusEvent> events)
    {
        RuntimeContext runtime = _runtimeFactory.Create(projectId);

        return new EventSubscriber(runtime.EventHandler, events);
    }
}
