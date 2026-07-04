namespace CodeSnifferDog.Models.Review;

/// <summary>
/// Creates normalized scope keys used by review and report workflows.
/// </summary>
public static class RuleScopeKeyFactory
{
    /// <summary>
    /// Creates a normalized <see cref="RuleFlowKey"/> for a repository, task item, and rule.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root used as the workflow scope.</param>
    /// <param name="projectPlanTaskItemId">Task-item identifier that owns the rule flow.</param>
    /// <param name="ruleKey">Rule key being reviewed.</param>
    /// <returns>The normalized rule-flow key.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is blank.</exception>
    public static RuleFlowKey CreateRuleFlowKey(
        string repositoryRootPath,
        string projectPlanTaskItemId,
        string ruleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPlanTaskItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);

        return new RuleFlowKey(
            repositoryRootPath.Trim(),
            projectPlanTaskItemId.Trim(),
            ruleKey.Trim());
    }

    /// <summary>
    /// Creates a normalized <see cref="RuleReportKey"/> for a repository and rule.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root used as the workflow scope.</param>
    /// <param name="ruleKey">Rule key whose repository report is being aggregated.</param>
    /// <returns>The normalized rule-report key.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is blank.</exception>
    public static RuleReportKey CreateRuleReportKey(
        string repositoryRootPath,
        string ruleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);

        return new RuleReportKey(
            repositoryRootPath.Trim(),
            ruleKey.Trim());
    }

    /// <summary>
    /// Creates the storage scope key used for review verdicts.
    /// </summary>
    /// <param name="ruleFlowKey">Rule-flow key that identifies the review scope.</param>
    /// <returns>The scoped storage key for review verdicts.</returns>
    public static string CreateReviewVerdictScopeKey(RuleFlowKey ruleFlowKey)
        => $"review-verdict::{ruleFlowKey.RepositoryRootPath}::{ruleFlowKey.ProjectPlanTaskItemId}::{ruleFlowKey.RuleKey}";

    /// <summary>
    /// Creates the storage scope key used for report verdicts.
    /// </summary>
    /// <param name="ruleFlowKey">Rule-flow key that identifies the report scope.</param>
    /// <returns>The scoped storage key for report verdicts.</returns>
    public static string CreateReportVerdictScopeKey(RuleFlowKey ruleFlowKey)
        => $"report-verdict::{ruleFlowKey.RepositoryRootPath}::{ruleFlowKey.ProjectPlanTaskItemId}::{ruleFlowKey.RuleKey}";
}
