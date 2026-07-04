namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;

/// <summary>
/// Restores project execution state after the host starts.
/// </summary>
internal interface IService
{
    /// <summary>
    /// Recovers projects that were interrupted before the host shut down.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the recovery operation.</param>
    Task RecoverAsync(CancellationToken cancellationToken);
}
