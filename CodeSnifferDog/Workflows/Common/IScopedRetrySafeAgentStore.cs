namespace CodeSnifferDog.Workflows.Common;

public interface IScopedRetrySafeAgentStore<TScope>
{
    IAgentAttemptLease BeginAttempt(TScope scope, Guid attemptId);
}
