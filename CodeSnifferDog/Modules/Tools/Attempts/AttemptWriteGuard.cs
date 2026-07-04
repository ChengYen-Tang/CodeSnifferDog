using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Attempts;

/// <summary>
/// Wraps <see cref="ScopedAttemptWriteGuard{TKey}" /> for a single default scope.
/// </summary>
internal sealed class AttemptWriteGuard
{
    private const string ScopeKey = "__default__";
    private readonly ScopedAttemptWriteGuard<string> _inner = new();

    /// <summary>
    /// Determines whether the current attempt may write to the default scope.
    /// </summary>
    /// <returns><see langword="true"/> when writes are allowed; otherwise, <see langword="false"/>.</returns>
    public bool CanWrite() =>
        _inner.CanWrite(ScopeKey);

    /// <summary>
    /// Begins a retry-safe attempt lease for the default scope.
    /// </summary>
    /// <param name="attemptId">Current attempt identifier.</param>
    /// <param name="restore">Callback that restores state when the lease is disposed.</param>
    /// <returns>The attempt lease.</returns>
    public IAgentAttemptLease BeginAttempt(Guid attemptId, Action restore) =>
        _inner.BeginAttempt(ScopeKey, attemptId, restore);
}
