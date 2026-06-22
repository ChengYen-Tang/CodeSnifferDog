using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.Review.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Review;

public sealed class ReviewVerdictBuffer
{
    private const string DefaultScopeKey = "__default__";
    private readonly ReviewVerdictStateStore _stateStore = new();
    private readonly ScopedAttemptWriteGuard<string> _writeGuard = new();
    private readonly Lock _syncRoot = new();

    public ReviewVerdict? Latest => GetLatest(DefaultScopeKey);

    public ReviewVerdict? GetLatest(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
            return _stateStore.GetLatest(scopeKey);
    }

    public void Reset() => Reset(DefaultScopeKey);

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

    public void Submit(bool approved, string message) => Submit(DefaultScopeKey, approved, message);

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

    internal IAgentAttemptLease BeginAttempt(Guid attemptId) => BeginAttempt(DefaultScopeKey, attemptId);

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
