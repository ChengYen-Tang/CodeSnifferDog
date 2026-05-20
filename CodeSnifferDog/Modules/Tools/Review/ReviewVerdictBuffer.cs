using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Review;

public sealed class ReviewVerdictBuffer
{
    private const string DefaultScopeKey = "__default__";
    private readonly Dictionary<string, ReviewVerdict> _latestByScope = [];
    private readonly Dictionary<string, Guid> _activeAttemptIdsByScope = [];
    private readonly Lock _syncRoot = new();

    public ReviewVerdict? Latest => GetLatest(DefaultScopeKey);

    public ReviewVerdict? GetLatest(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
            return _latestByScope.GetValueOrDefault(scopeKey.Trim());
    }

    public void Reset() => Reset(DefaultScopeKey);

    public void Reset(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
        {
            if (!CanWrite(scopeKey.Trim()))
                return;

            _latestByScope.Remove(scopeKey.Trim());
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
            if (!CanWrite(normalizedScopeKey))
                return;

            _latestByScope[normalizedScopeKey] = new ReviewVerdict
            {
                Approved = approved,
                Message = message,
            };
        }
    }

    internal IAgentAttemptLease BeginAttempt(Guid attemptId) => BeginAttempt(DefaultScopeKey, attemptId);

    internal IAgentAttemptLease BeginAttempt(string scopeKey, Guid attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
        {
            string normalizedScopeKey = scopeKey.Trim();
            bool hadVerdict = _latestByScope.TryGetValue(normalizedScopeKey, out ReviewVerdict? previousVerdict);
            Guid staleWriteBlockerAttemptId = Guid.NewGuid();
            ReviewVerdict? clonedVerdict = previousVerdict is null
                ? null
                : new ReviewVerdict
                {
                    Approved = previousVerdict.Approved,
                    Message = previousVerdict.Message,
                };

            _activeAttemptIdsByScope[normalizedScopeKey] = attemptId;

            return new AgentAttemptLease(() =>
            {
                lock (_syncRoot)
                {
                    _activeAttemptIdsByScope[normalizedScopeKey] = staleWriteBlockerAttemptId;

                    if (clonedVerdict is null)
                        _latestByScope.Remove(normalizedScopeKey);
                    else
                        _latestByScope[normalizedScopeKey] = new ReviewVerdict
                        {
                            Approved = clonedVerdict.Approved,
                            Message = clonedVerdict.Message,
                        };
                }
            });
        }
    }

    private bool CanWrite(string scopeKey)
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null ||
            !_activeAttemptIdsByScope.TryGetValue(scopeKey, out Guid activeAttemptId) ||
            currentAttemptId == activeAttemptId;
    }

    internal sealed class Snapshot(
        IReadOnlyDictionary<string, Guid> activeAttemptIdsByScope,
        IReadOnlyDictionary<string, ReviewVerdict> latestByScope)
    {
        public IReadOnlyDictionary<string, Guid> ActiveAttemptIdsByScope { get; } = activeAttemptIdsByScope;

        public IReadOnlyDictionary<string, ReviewVerdict> LatestByScope { get; } = latestByScope;
    }
}
