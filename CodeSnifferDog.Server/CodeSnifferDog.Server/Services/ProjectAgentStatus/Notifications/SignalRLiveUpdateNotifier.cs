using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;

public sealed class SignalRLiveUpdateNotifier(IHubContext<ProjectUpdatesHub> hubContext) : ILiveUpdateNotifier
{
    private readonly IHubContext<ProjectUpdatesHub> _hubContext = hubContext;

    public Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        Guid? agentId = update.Kind switch
        {
            LiveUpdateKind.TimelineEntryUpserted => update.TimelineEntry?.AgentId,
            LiveUpdateKind.TimelineEntriesRemoved => update.RemovedTimelineEntries?.AgentId,
            _ => null,
        };

        return agentId is Guid projectAgentId
            ? _hubContext.Clients
                .Group(ProjectUpdatesContract.GetProjectAgentChannelName(update.ProjectId, projectAgentId))
                .SendAsync(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update, cancellationToken)
            : _hubContext.Clients
                .Group(ProjectUpdatesContract.GetProjectChannelName(update.ProjectId))
                .SendAsync(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update, cancellationToken);
    }
}
