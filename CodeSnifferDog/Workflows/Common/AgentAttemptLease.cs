namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Wraps one restore callback so it can be invoked at most once per attempt.
/// </summary>
/// <param name="restore">Callback that restores state captured before the attempt ran.</param>
internal sealed class AgentAttemptLease(Action restore) : IAgentAttemptLease
{
    private readonly Action _restore = restore;
    private int _restored;

    /// <inheritdoc />
    public void Restore()
    {
        if (Interlocked.Exchange(ref _restored, 1) == 0)
            _restore();
    }
}
