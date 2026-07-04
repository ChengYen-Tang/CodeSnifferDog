using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

/// <summary>
/// Handles project uploads, project lookup, cancellation, and deletion.
/// </summary>
public interface IProjectIntakeService
{
    /// <summary>
    /// Uploads one project zip file and queues it for processing.
    /// </summary>
    /// <param name="zipFile">Uploaded project zip file.</param>
    /// <param name="cancellationToken">Cancels upload and queueing.</param>
    /// <returns>The queued project upload result.</returns>
    Task<ProjectUploadResult> UploadAsync(IFormFile zipFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists queued and historical projects.
    /// </summary>
    /// <param name="cancellationToken">Cancels listing.</param>
    /// <returns>The project list.</returns>
    Task<IReadOnlyList<ProjectListItemDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the summary for one project.
    /// </summary>
    /// <param name="projectId">Project identifier to load.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The project summary, or <see langword="null" /> when the project is absent.</returns>
    Task<ProjectSummaryDto?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation of one running project.
    /// </summary>
    /// <param name="projectId">Project identifier to cancel.</param>
    /// <param name="cancellationToken">Cancels the cancellation request.</param>
    /// <returns><see langword="true" /> when the project exists and cancellation was requested; otherwise <see langword="false" />.</returns>
    Task<bool> CancelAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one project and its temporary storage.
    /// </summary>
    /// <param name="projectId">Project identifier to delete.</param>
    /// <param name="cancellationToken">Cancels deletion.</param>
    /// <returns><see langword="true" /> when the project existed and was deleted; otherwise <see langword="false" />.</returns>
    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}
