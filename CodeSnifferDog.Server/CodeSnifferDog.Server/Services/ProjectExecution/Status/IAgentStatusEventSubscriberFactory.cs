using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal interface IAgentStatusEventSubscriberFactory
{
    ProjectAgentStatusEventSubscriber Create(
        Guid projectId,
        IObservable<AgentStatusEvent> events);
}
