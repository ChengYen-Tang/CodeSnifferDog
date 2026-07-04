namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Combines store and verdict leases so one failed workflow attempt can restore both pieces of mutable state together.
/// </summary>
/// <param name="storeLease">Lease that restores persisted workflow state.</param>
/// <param name="verdictLease">Lease that restores verdict-buffer state.</param>
internal sealed class WorkflowAttemptLeasePair(
    IAgentAttemptLease storeLease,
    IAgentAttemptLease verdictLease)
{
    private readonly IAgentAttemptLease _storeLease = storeLease;
    private readonly IAgentAttemptLease _verdictLease = verdictLease;

    /// <summary>
    /// Restores both leases in a deterministic order.
    /// </summary>
    public void Restore()
    {
        _storeLease.Restore();
        _verdictLease.Restore();
    }
}
