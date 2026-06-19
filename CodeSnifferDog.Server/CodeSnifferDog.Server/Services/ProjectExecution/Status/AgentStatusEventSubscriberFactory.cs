using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusEventSubscriberFactory(IAgentStatusRuntimeFactory runtimeFactory)
    : IAgentStatusEventSubscriberFactory
{
    private readonly IAgentStatusRuntimeFactory _runtimeFactory = runtimeFactory;

    public ProjectAgentStatusEventSubscriber Create(
        Guid projectId,
        IObservable<AgentStatusEvent> events)
    {
        AgentStatusRuntime runtime = _runtimeFactory.Create(projectId);

        return new ProjectAgentStatusEventSubscriber(runtime.EventHandler, events);
    }
}
