using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal interface IAgentStatusEventSubscriberFactory
{
    ProjectAgentStatusEventSubscriber Create(
        Guid projectId,
        IObservable<AgentStatusEvent> events);
}
