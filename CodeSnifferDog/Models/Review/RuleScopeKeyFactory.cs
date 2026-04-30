namespace CodeSnifferDog.Models.Review;

public static class RuleScopeKeyFactory
{
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

    public static string CreateReviewVerdictScopeKey(RuleFlowKey ruleFlowKey)
        => $"review-verdict::{ruleFlowKey.RepositoryRootPath}::{ruleFlowKey.ProjectPlanTaskItemId}::{ruleFlowKey.RuleKey}";

    public static string CreateReportVerdictScopeKey(RuleFlowKey ruleFlowKey)
        => $"report-verdict::{ruleFlowKey.RepositoryRootPath}::{ruleFlowKey.ProjectPlanTaskItemId}::{ruleFlowKey.RuleKey}";
}
