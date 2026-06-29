namespace CodeSnifferDog.Workflows.Common;

internal sealed class WorkflowAttemptLeasePair(
    IAgentAttemptLease storeLease,
    IAgentAttemptLease verdictLease)
{
    private readonly IAgentAttemptLease _storeLease = storeLease;
    private readonly IAgentAttemptLease _verdictLease = verdictLease;

    public void Restore()
    {
        _storeLease.Restore();
        _verdictLease.Restore();
    }
}
