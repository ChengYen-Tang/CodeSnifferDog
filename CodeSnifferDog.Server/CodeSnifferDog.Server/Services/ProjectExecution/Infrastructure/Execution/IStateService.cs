using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

/// <summary>
/// Persists project execution state transitions and publishes status updates.
/// </summary>
internal interface IStateService
{
    /// <summary>
    /// Determines whether execution may still start for the specified project.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <param name="cancellationToken">Token that cancels the database operation.</param>
    /// <returns><see langword="true"/> when execution may proceed; otherwise, <see langword="false"/>.</returns>
    Task<bool> CanStartExecutionAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a project execution as complete and persists its terminal status.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <param name="status">Terminal project status to persist.</param>
    /// <param name="failureReason">Optional failure reason to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task CompleteAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        string? failureReason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a project status update to live subscribers.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <param name="status">Current project status.</param>
    /// <param name="cancellationToken">Token that cancels the notification.</param>
    Task PublishStatusUpdateAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        CancellationToken cancellationToken);
}
