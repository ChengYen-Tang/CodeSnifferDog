using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Attempts;

/// <summary>
/// Blocks stale attempts from writing after a newer retry attempt has started.
/// </summary>
/// <typeparam name="TKey">Scope key type used to partition write ownership.</typeparam>
internal sealed class ScopedAttemptWriteGuard<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, Guid> _activeAttemptIds = [];

    /// <summary>
    /// Determines whether the current attempt may write to the specified scope.
    /// </summary>
    /// <param name="key">Scope key.</param>
    /// <returns><see langword="true"/> when writes are allowed; otherwise, <see langword="false"/>.</returns>
    public bool CanWrite(TKey key)
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null ||
            !_activeAttemptIds.TryGetValue(key, out Guid activeAttemptId) ||
            currentAttemptId == activeAttemptId;
    }

    /// <summary>
    /// Begins a retry-safe attempt lease for one scope.
    /// </summary>
    /// <param name="key">Scope key.</param>
    /// <param name="attemptId">Current attempt identifier.</param>
    /// <param name="restore">Callback that restores state when the lease is disposed.</param>
    /// <returns>The attempt lease.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="restore"/> is <see langword="null"/>.</exception>
    public IAgentAttemptLease BeginAttempt(TKey key, Guid attemptId, Action restore)
    {
        ArgumentNullException.ThrowIfNull(restore);

        Guid staleWriteBlockerAttemptId = Guid.NewGuid();
        _activeAttemptIds[key] = attemptId;

        return new AgentAttemptLease(() =>
        {
            _activeAttemptIds[key] = staleWriteBlockerAttemptId;
            restore();
        });
    }

    /// <summary>
    /// Clears attempt ownership for one scope.
    /// </summary>
    /// <param name="key">Scope key.</param>
    public void Clear(TKey key) =>
        _activeAttemptIds.Remove(key);
}
