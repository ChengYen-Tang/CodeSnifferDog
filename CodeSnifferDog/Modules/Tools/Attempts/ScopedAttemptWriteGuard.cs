using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Attempts;

internal sealed class ScopedAttemptWriteGuard<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, Guid> _activeAttemptIds = [];

    public bool CanWrite(TKey key)
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null ||
            !_activeAttemptIds.TryGetValue(key, out Guid activeAttemptId) ||
            currentAttemptId == activeAttemptId;
    }

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

    public void Clear(TKey key) =>
        _activeAttemptIds.Remove(key);
}
