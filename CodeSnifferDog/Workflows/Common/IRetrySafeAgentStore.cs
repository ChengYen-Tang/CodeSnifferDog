namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Exposes retry-safe rollback for stores that have a single implicit scope.
/// </summary>
public interface IRetrySafeAgentStore
{
    /// <summary>
    /// Begins an attempt lease that can restore store state if the attempt is abandoned.
    /// </summary>
    /// <param name="attemptId">Current workflow attempt identifier.</param>
    /// <returns>The attempt lease.</returns>
    IAgentAttemptLease BeginAttempt(Guid attemptId);
}
