using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;

/// <summary>
/// Broadcasts live agent-status updates to connected clients.
/// </summary>
public interface ILiveUpdateNotifier
{
    /// <summary>
    /// Publishes one live update to interested clients.
    /// </summary>
    /// <param name="update">Live update payload to broadcast.</param>
    /// <param name="cancellationToken">Cancels notification publication.</param>
    Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default);
}
