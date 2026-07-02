using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;

public interface ILiveUpdateNotifier
{
    Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default);
}
