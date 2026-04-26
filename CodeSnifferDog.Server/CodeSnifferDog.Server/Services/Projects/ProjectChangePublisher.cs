using CodeSnifferDog.Server.Services.ProjectIntake;

namespace CodeSnifferDog.Server.Services.Projects;

public sealed class ProjectChangePublisher(
    IProjectUpdatesNotifier projectUpdatesNotifier,
    ILogger<ProjectChangePublisher> logger) : IProjectChangePublisher
{
    private readonly IProjectUpdatesNotifier _projectUpdatesNotifier = projectUpdatesNotifier;
    private readonly ILogger<ProjectChangePublisher> _logger = logger;

    public async Task PublishProjectsChangedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectUpdatesNotifier.NotifyProjectsChangedAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Project change was saved, but project update notification failed.");
        }
    }
}
