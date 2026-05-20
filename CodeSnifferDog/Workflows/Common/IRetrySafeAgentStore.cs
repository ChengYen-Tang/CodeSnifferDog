namespace CodeSnifferDog.Workflows.Common;

public interface IRetrySafeAgentStore
{
    IAgentAttemptLease BeginAttempt(Guid attemptId);
}
