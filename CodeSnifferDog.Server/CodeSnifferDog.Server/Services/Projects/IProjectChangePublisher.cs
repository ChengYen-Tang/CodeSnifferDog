namespace CodeSnifferDog.Server.Services.Projects;

/// <summary>
/// Publishes project-list change notifications to interested clients.
/// </summary>
public interface IProjectChangePublisher
{
    /// <summary>
    /// Publishes that the project list changed.
    /// </summary>
    /// <param name="cancellationToken">Cancels notification publication.</param>
    Task PublishProjectsChangedAsync(CancellationToken cancellationToken = default);
}
