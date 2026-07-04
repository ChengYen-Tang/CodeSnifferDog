using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.Review.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Review;

/// <summary>
/// Stores verifier verdicts with retry-safe rollback support.
/// </summary>
public sealed class ReviewVerdictBuffer
{
    private const string DefaultScopeKey = "__default__";
    private readonly ReviewVerdictStateStore _stateStore = new();
    private readonly ScopedAttemptWriteGuard<string> _writeGuard = new();
    private readonly Lock _syncRoot = new();

    /// <summary>
    /// Gets the latest default-scope verdict.
    /// </summary>
    public ReviewVerdict? Latest => GetLatest(DefaultScopeKey);

    /// <summary>
    /// Gets the latest verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <returns>The latest verdict, or <see langword="null"/> when none exists.</returns>
    public ReviewVerdict? GetLatest(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
            return _stateStore.GetLatest(scopeKey);
    }

    /// <summary>
    /// Resets the default-scope verdict.
    /// </summary>
    public void Reset() => Reset(DefaultScopeKey);

    /// <summary>
    /// Resets the verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    public void Reset(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
        {
            string normalizedScopeKey = scopeKey.Trim();
            if (!_writeGuard.CanWrite(normalizedScopeKey))
                return;

            _stateStore.Reset(normalizedScopeKey);
        }
    }

    /// <summary>
    /// Stores the default-scope verdict.
    /// </summary>
    /// <param name="approved">Whether the verdict approved the work.</param>
    /// <param name="message">Verdict message.</param>
    public void Submit(bool approved, string message) => Submit(DefaultScopeKey, approved, message);

    /// <summary>
    /// Stores the verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <param name="approved">Whether the verdict approved the work.</param>
    /// <param name="message">Verdict message.</param>
    public void Submit(string scopeKey, bool approved, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        lock (_syncRoot)
        {
            string normalizedScopeKey = scopeKey.Trim();
            if (!_writeGuard.CanWrite(normalizedScopeKey))
                return;

            _stateStore.Submit(normalizedScopeKey, approved, message);
        }
    }

    /// <summary>
    /// Begins a retry-safe attempt lease for the default scope.
    /// </summary>
    /// <param name="attemptId">Current attempt identifier.</param>
    /// <returns>The attempt lease.</returns>
    internal IAgentAttemptLease BeginAttempt(Guid attemptId) => BeginAttempt(DefaultScopeKey, attemptId);

    /// <summary>
    /// Begins a retry-safe attempt lease for one verdict scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <param name="attemptId">Current attempt identifier.</param>
    /// <returns>The attempt lease.</returns>
    internal IAgentAttemptLease BeginAttempt(string scopeKey, Guid attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
        {
            string normalizedScopeKey = scopeKey.Trim();
            ReviewVerdict? snapshot = _stateStore.Clone(normalizedScopeKey);
            return _writeGuard.BeginAttempt(
                normalizedScopeKey,
                attemptId,
                () => _stateStore.Restore(normalizedScopeKey, snapshot));
        }
    }
}
