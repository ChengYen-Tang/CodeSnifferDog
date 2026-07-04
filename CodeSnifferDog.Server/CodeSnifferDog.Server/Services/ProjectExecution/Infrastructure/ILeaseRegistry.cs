namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Tracks active project execution leases and coordinates cancellation requests.
/// </summary>
public interface ILeaseRegistry
{
    /// <summary>
    /// Registers a running project execution and returns its lease.
    /// </summary>
    /// <param name="projectId">Project identifier being executed.</param>
    /// <param name="cancellationToken">Host cancellation token that represents service shutdown.</param>
    /// <returns>The lease for the running project.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the project is already registered.</exception>
    Lease Register(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the running project, if any, and waits for its execution lease to complete.
    /// </summary>
    /// <param name="projectId">Project identifier to cancel.</param>
    /// <param name="cancellationToken">Token that cancels waiting for completion.</param>
    /// <returns><see langword="true"/> when a running project was canceled; otherwise, <see langword="false"/>.</returns>
    Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to cancel a running project without waiting for completion.
    /// </summary>
    /// <param name="projectId">Project identifier to cancel.</param>
    /// <param name="completion">Completion task for the running lease when cancellation succeeds.</param>
    /// <returns><see langword="true"/> when cancellation was requested; otherwise, <see langword="false"/>.</returns>
    bool TryCancel(Guid projectId, out Task? completion);
}
