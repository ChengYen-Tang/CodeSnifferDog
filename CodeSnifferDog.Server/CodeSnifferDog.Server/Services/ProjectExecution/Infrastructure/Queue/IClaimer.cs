namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

/// <summary>
/// Claims queued projects so background workers can start execution.
/// </summary>
internal interface IClaimer
{
    /// <summary>
    /// Attempts to claim the next queued project.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the claim operation.</param>
    /// <returns>The claimed project, or <see langword="null"/> when the queue is empty.</returns>
    Task<Claim?> TryClaimNextAsync(CancellationToken cancellationToken);
}
