using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Hubs;

public sealed class ProjectUpdatesHub : Hub
{
    private readonly IProjectAgentStatusLiveBackfillService _backfillService;

    public ProjectUpdatesHub(IProjectAgentStatusLiveBackfillService backfillService)
    {
        _backfillService = backfillService;
    }

    public async Task SubscribeToProject(ProjectAgentLiveSubscriptionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CancellationToken cancellationToken = Context.ConnectionAborted;

        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectUpdatesContract.GetProjectChannelName(request.ProjectId), cancellationToken);

        IReadOnlyList<ProjectAgentLiveUpdateDto> backfill = await _backfillService.GetBackfillAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (ProjectAgentLiveUpdateDto update in backfill)
        {
            await Clients.Caller.SendAsync(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task UnsubscribeFromProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectUpdatesContract.GetProjectChannelName(projectId));
}
