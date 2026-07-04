namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Represents the lease that restores store state when a workflow attempt must roll back.
/// </summary>
public interface IAgentAttemptLease
{
    /// <summary>
    /// Restores the store state captured when the lease began.
    /// </summary>
    void Restore();
}
