namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Exposes retry-safe rollback for stores that partition state by a scope key.
/// </summary>
/// <typeparam name="TScope">Scope key type that identifies one store partition.</typeparam>
public interface IScopedRetrySafeAgentStore<TScope>
{
    /// <summary>
    /// Begins an attempt lease for one scoped partition.
    /// </summary>
    /// <param name="scope">Scope whose state is owned by the attempt.</param>
    /// <param name="attemptId">Current workflow attempt identifier.</param>
    /// <returns>The attempt lease.</returns>
    IAgentAttemptLease BeginAttempt(TScope scope, Guid attemptId);
}
