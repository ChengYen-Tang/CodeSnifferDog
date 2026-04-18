namespace CodeSnifferDog.Models.Review;

public readonly record struct RuleFlowKey(
    string RepositoryRootPath,
    string ProjectPlanTaskItemId,
    string RuleKey);
