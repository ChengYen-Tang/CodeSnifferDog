using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Models.Review;

public static class RuleScopeKeyFactory
{
    public static RuleFlowKey CreateRuleFlowKey(
        string repositoryRootPath,
        string projectPlanTaskItemId,
        string ruleMarkdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPlanTaskItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleMarkdown);

        return new RuleFlowKey(
            repositoryRootPath.Trim(),
            projectPlanTaskItemId.Trim(),
            CreateRuleKey(ruleMarkdown));
    }

    public static RuleReportKey CreateRuleReportKey(
        string repositoryRootPath,
        string ruleMarkdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleMarkdown);

        return new RuleReportKey(
            repositoryRootPath.Trim(),
            CreateRuleKey(ruleMarkdown));
    }

    public static string CreateReviewVerdictScopeKey(RuleFlowKey ruleFlowKey)
        => $"review-verdict::{ruleFlowKey.RepositoryRootPath}::{ruleFlowKey.ProjectPlanTaskItemId}::{ruleFlowKey.RuleKey}";

    public static string CreateReportVerdictScopeKey(RuleFlowKey ruleFlowKey)
        => $"report-verdict::{ruleFlowKey.RepositoryRootPath}::{ruleFlowKey.ProjectPlanTaskItemId}::{ruleFlowKey.RuleKey}";

    private static string CreateRuleKey(string ruleMarkdown)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(ruleMarkdown.Trim()));
        return Convert.ToHexStringLower(hash);
    }
}
