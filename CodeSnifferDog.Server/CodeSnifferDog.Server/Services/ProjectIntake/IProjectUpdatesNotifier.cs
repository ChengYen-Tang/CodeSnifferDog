namespace CodeSnifferDog.Server.Services.ProjectIntake;

/// <summary>
/// Broadcasts project-list change notifications to connected clients.
/// </summary>
public interface IProjectUpdatesNotifier
{
    /// <summary>
    /// Notifies clients that the project list changed.
    /// </summary>
    /// <param name="cancellationToken">Cancels notification publication.</param>
    Task NotifyProjectsChangedAsync(CancellationToken cancellationToken = default);
}
