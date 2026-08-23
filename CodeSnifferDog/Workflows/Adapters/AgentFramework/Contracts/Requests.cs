using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Workflows.Adapters.AgentFramework.Contracts;

/// <summary>
/// Carries the input required to execute the scan workflow through Agent Framework.
/// </summary>
internal sealed record ScanRequest(string RepositoryRootPath);

/// <summary>
/// Carries the input required to execute the project-plan workflow through Agent Framework.
/// </summary>
internal sealed record ProjectPlanRequest(string RepositoryRootPath, StoredScanProject ScanProject);

/// <summary>
/// Carries the input required to execute the combined rule flow through Agent Framework.
/// </summary>
internal sealed record RuleFlowRequest(
    string RepositoryRootPath,
    string RuleKey,
    string RuleMarkdown,
    StoredTaskItem TaskItem);

/// <summary>
/// Carries the input required to execute the rule-review workflow through Agent Framework.
/// </summary>
internal sealed record RuleReviewRequest(
    string RepositoryRootPath,
    string RuleKey,
    string RuleMarkdown,
    StoredTaskItem TaskItem);

/// <summary>
/// Carries the input required to execute the report workflow through Agent Framework.
/// </summary>
internal sealed record ReportRequest(
    string RepositoryRootPath,
    string RuleKey,
    string RuleMarkdown,
    StoredTaskItem TaskItem,
    IReadOnlyList<RuleReviewStoredIssue> CurrentFlowIssues);
