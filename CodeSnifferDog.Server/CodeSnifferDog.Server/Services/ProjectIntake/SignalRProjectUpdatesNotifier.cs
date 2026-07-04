using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

/// <summary>
/// Broadcasts project-list change notifications over SignalR.
/// </summary>
/// <param name="hubContext">SignalR hub context used to notify connected clients.</param>
public sealed class SignalRProjectUpdatesNotifier(IHubContext<ProjectUpdatesHub> hubContext) : IProjectUpdatesNotifier
{
    private readonly IHubContext<ProjectUpdatesHub> _hubContext = hubContext;

    /// <inheritdoc />
    public Task NotifyProjectsChangedAsync(CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync(ProjectUpdatesContract.ProjectsChangedMethodName, cancellationToken);
}
