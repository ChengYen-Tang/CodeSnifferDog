namespace CodeSnifferDog.Workflows.Common;

internal sealed class AgentAttemptLease(Action restore) : IAgentAttemptLease
{
    private readonly Action _restore = restore;
    private int _restored;

    public void Restore()
    {
        if (Interlocked.Exchange(ref _restored, 1) == 0)
            _restore();
    }
}
