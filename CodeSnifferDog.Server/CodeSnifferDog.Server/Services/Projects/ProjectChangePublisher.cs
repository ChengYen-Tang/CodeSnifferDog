using CodeSnifferDog.Server.Services.ProjectIntake;

namespace CodeSnifferDog.Server.Services.Projects;

/// <summary>
/// Publishes project-change notifications while swallowing notification failures after the write already succeeded.
/// </summary>
/// <param name="projectUpdatesNotifier">Notifier used to broadcast project-list changes.</param>
/// <param name="logger">Logger used to record notification failures.</param>
public sealed class ProjectChangePublisher(
    IProjectUpdatesNotifier projectUpdatesNotifier,
    ILogger<ProjectChangePublisher> logger) : IProjectChangePublisher
{
    private readonly IProjectUpdatesNotifier _projectUpdatesNotifier = projectUpdatesNotifier;
    private readonly ILogger<ProjectChangePublisher> _logger = logger;

    /// <inheritdoc />
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
