using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;

public interface ILiveUpdateNotifier
{
    Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default);
}
