using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

public sealed class SignalRProjectUpdatesNotifier(IHubContext<ProjectUpdatesHub> hubContext) : IProjectUpdatesNotifier
{
    private readonly IHubContext<ProjectUpdatesHub> _hubContext = hubContext;

    public Task NotifyProjectsChangedAsync(CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync(ProjectUpdatesContract.ProjectsChangedMethodName, cancellationToken);
}
