namespace CodeSnifferDog.Models.Review;

/// <summary>
/// Identifies one rule-flow execution within a repository and task item.
/// </summary>
/// <param name="RepositoryRootPath">Repository root used as the workflow scope.</param>
/// <param name="ProjectPlanTaskItemId">Task-item identifier that owns the rule flow.</param>
/// <param name="RuleKey">Rule key being reviewed.</param>
public readonly record struct RuleFlowKey(
    string RepositoryRootPath,
    string ProjectPlanTaskItemId,
    string RuleKey);
