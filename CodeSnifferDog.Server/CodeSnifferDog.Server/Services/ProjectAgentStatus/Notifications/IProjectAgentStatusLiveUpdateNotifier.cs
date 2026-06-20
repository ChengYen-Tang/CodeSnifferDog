using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;

public interface IProjectAgentStatusLiveUpdateNotifier
{
    Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default);
}
