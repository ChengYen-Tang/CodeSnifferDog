using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public sealed class SignalRProjectAgentStatusLiveUpdateNotifier(IHubContext<ProjectUpdatesHub> hubContext) : IProjectAgentStatusLiveUpdateNotifier
{
    private readonly IHubContext<ProjectUpdatesHub> _hubContext = hubContext;

    public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return _hubContext.Clients
            .Group(ProjectUpdatesContract.GetProjectChannelName(update.ProjectId))
            .SendAsync(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update, cancellationToken);
    }
}
