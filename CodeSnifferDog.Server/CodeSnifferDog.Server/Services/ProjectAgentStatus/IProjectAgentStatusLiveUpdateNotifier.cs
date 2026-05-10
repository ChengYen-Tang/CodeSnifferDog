using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public interface IProjectAgentStatusLiveUpdateNotifier
{
    Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default);
}
