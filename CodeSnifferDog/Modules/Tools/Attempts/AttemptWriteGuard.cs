using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Attempts;

internal sealed class AttemptWriteGuard
{
    private const string ScopeKey = "__default__";
    private readonly ScopedAttemptWriteGuard<string> _inner = new();

    public bool CanWrite() =>
        _inner.CanWrite(ScopeKey);

    public IAgentAttemptLease BeginAttempt(Guid attemptId, Action restore) =>
        _inner.BeginAttempt(ScopeKey, attemptId, restore);
}
