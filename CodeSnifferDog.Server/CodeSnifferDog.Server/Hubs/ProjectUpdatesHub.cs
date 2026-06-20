using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Hubs;

public sealed class ProjectUpdatesHub : Hub
{
    private readonly IProjectAgentStatusLiveBackfillService _backfillService;
    private const string CurrentTimelineProjectIdKey = "agent-status-current-project-id";
    private const string CurrentTimelineAgentIdKey = "agent-status-current-agent-id";

    public ProjectUpdatesHub(IProjectAgentStatusLiveBackfillService backfillService)
    {
        _backfillService = backfillService;
    }

    public async Task SubscribeToProject(ProjectAgentLiveSubscriptionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CancellationToken cancellationToken = Context.ConnectionAborted;

        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectUpdatesContract.GetProjectChannelName(request.ProjectId), cancellationToken);
        await SwapAgentTimelineGroupAsync(request.ProjectId, request.AgentId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ProjectAgentLiveUpdateDto> backfill = await _backfillService.GetBackfillAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (ProjectAgentLiveUpdateDto update in backfill)
        {
            await Clients.Caller.SendAsync(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UnsubscribeFromProject(Guid projectId)
    {
        CancellationToken cancellationToken = Context.ConnectionAborted;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectUpdatesContract.GetProjectChannelName(projectId), cancellationToken);
        await SwapAgentTimelineGroupAsync(projectId, agentId: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task SwapAgentTimelineGroupAsync(Guid projectId, Guid? agentId, CancellationToken cancellationToken)
    {
        if (Context.Items.TryGetValue(CurrentTimelineProjectIdKey, out object? previousProjectIdValue)
            && previousProjectIdValue is Guid previousProjectId
            && Context.Items.TryGetValue(CurrentTimelineAgentIdKey, out object? previousAgentIdValue)
            && previousAgentIdValue is Guid previousAgentId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                ProjectUpdatesContract.GetProjectAgentChannelName(previousProjectId, previousAgentId),
                cancellationToken);
        }

        if (agentId is Guid nextAgentId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                ProjectUpdatesContract.GetProjectAgentChannelName(projectId, nextAgentId),
                cancellationToken);
            Context.Items[CurrentTimelineProjectIdKey] = projectId;
            Context.Items[CurrentTimelineAgentIdKey] = nextAgentId;
            return;
        }

        Context.Items.Remove(CurrentTimelineProjectIdKey);
        Context.Items.Remove(CurrentTimelineAgentIdKey);
    }
}
