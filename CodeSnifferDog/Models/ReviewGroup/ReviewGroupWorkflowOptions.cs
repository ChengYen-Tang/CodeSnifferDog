namespace CodeSnifferDog.Models.ReviewGroup;

public sealed class ReviewGroupWorkflowOptions
{
    public int MaxConcurrentRuleFlows { get; init; } = 4;
}
