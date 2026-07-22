using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Hubs;

/// <summary>
/// SignalR hub that manages live project and agent-status subscriptions for connected clients.
/// </summary>
public sealed class ProjectUpdatesHub : Hub
{
    private readonly ILiveBackfillService _backfillService;
    private const string CurrentTimelineProjectIdKey = "agent-status-current-project-id";
    private const string CurrentTimelineAgentIdKey = "agent-status-current-agent-id";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectUpdatesHub"/> class.
    /// </summary>
    /// <param name="backfillService">Backfill service used to replay recent live updates after subscription.</param>
    public ProjectUpdatesHub(ILiveBackfillService backfillService)
    {
        _backfillService = backfillService;
    }

    /// <summary>
    /// Subscribes the caller to project-wide updates and an optional agent-specific timeline channel.
    /// </summary>
    /// <param name="request">Subscription request that identifies the project and optional agent timeline.</param>
    /// <returns>The ordered catch-up updates captured after group membership is established.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<LiveUpdateDto[]> SubscribeToProject(LiveSubscriptionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CancellationToken cancellationToken = Context.ConnectionAborted;

        try
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                ProjectUpdatesContract.GetProjectChannelName(request.ProjectId),
                cancellationToken);
            await SwapAgentTimelineGroupAsync(request.ProjectId, request.AgentId, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<LiveUpdateDto> backfill = await _backfillService
                .GetBackfillAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return [.. backfill];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    /// <summary>
    /// Unsubscribes the caller from project-wide updates and any currently selected agent timeline.
    /// </summary>
    /// <param name="projectId">Project identifier whose live update groups should be removed.</param>
    /// <returns>A task that completes after the caller is removed from the relevant groups.</returns>
    public async Task UnsubscribeFromProject(Guid projectId)
    {
        CancellationToken cancellationToken = Context.ConnectionAborted;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectUpdatesContract.GetProjectChannelName(projectId), cancellationToken);
        await SwapAgentTimelineGroupAsync(projectId, agentId: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Swaps the caller's agent-specific timeline subscription while preserving the project-wide channel.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the target agent timeline.</param>
    /// <param name="agentId">Agent identifier to subscribe to, or <see langword="null"/> to clear the agent timeline subscription.</param>
    /// <param name="cancellationToken">Token that cancels the SignalR group operations.</param>
    /// <returns>A task that completes after the relevant group membership changes are applied.</returns>
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
