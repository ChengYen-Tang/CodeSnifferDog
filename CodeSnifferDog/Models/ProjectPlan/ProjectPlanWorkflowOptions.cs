namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class ProjectPlanWorkflowOptions
{
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    public int MaxProjectPlanAgentResets { get; init; } = 3;
}
