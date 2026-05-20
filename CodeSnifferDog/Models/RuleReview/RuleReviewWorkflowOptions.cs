namespace CodeSnifferDog.Models.RuleReview;

public sealed class RuleReviewWorkflowOptions
{
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    public int MaxRuleReviewAgentResets { get; init; } = 3;

    public int MaxConsecutiveRunFailures { get; init; } = Scan.AgentExecutionOptionsDefaults.MaxConsecutiveRunFailures;

    public TimeSpan AgentRunTimeout { get; init; } = Scan.AgentExecutionOptionsDefaults.AgentRunTimeout;
}
