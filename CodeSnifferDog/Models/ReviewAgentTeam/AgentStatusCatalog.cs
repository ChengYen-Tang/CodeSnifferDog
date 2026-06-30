using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

public static class AgentStatusCatalog
{
    public const string WaitingStatus = "Waiting";
    public const string RunningStatus = "Running";
    public const string CompletedStatus = "Completed";
    public const string DegradedStatus = "Degraded";

    public static string CreateScanGroupKey() => "scan";

    public static string CreateScanGroupDisplayName() => "Repository Scan";

    public static string CreateScanAgentKey() => "scan:scan";

    public static string CreateScanAgentDisplayName() => "Scan";

    public static string CreateScanVerifierAgentKey() => "scan:scan-verifier";

    public static string CreateScanVerifierAgentDisplayName() => "Scan Verifier";

    public static string CreateProjectPlanGroupKey(StoredScanProject scanProject) =>
        $"project-plan:{scanProject.ScanProjectId}";

    public static string CreateProjectPlanGroupDisplayName(StoredScanProject scanProject) =>
        $"Project Plan: {scanProject.ProjectName}";

    public static string CreateProjectPlannerAgentKey(StoredScanProject scanProject) =>
        $"project-plan:{scanProject.ScanProjectId}:planner";

    public static string CreateProjectPlannerAgentDisplayName() => "Project Plan";

    public static string CreateProjectVerifierAgentKey(StoredScanProject scanProject) =>
        $"project-plan:{scanProject.ScanProjectId}:verifier";

    public static string CreateProjectVerifierAgentDisplayName() => "Project Verifier";

    public static string CreateReviewTaskGroupKey(StoredProjectPlanTaskItem taskItem) =>
        $"review-task:{taskItem.ProjectPlanTaskItemId}";

    public static string CreateReviewTaskGroupDisplayName(int reviewNumber) =>
        $"Review: {reviewNumber}";

    public static string CreateRuleReviewAgentKey(StoredProjectPlanTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:rule-review";

    public static string CreateRuleReviewAgentDisplayName(string ruleKey) =>
        $"Rule Review · {ruleKey}";

    public static string CreateReviewVerifierAgentKey(StoredProjectPlanTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:review-verifier";

    public static string CreateReviewVerifierAgentDisplayName(string ruleKey) =>
        $"Review Verifier · {ruleKey}";

    public static string CreateReportAggregatorAgentKey(StoredProjectPlanTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:report-aggregator";

    public static string CreateReportAggregatorAgentDisplayName(string ruleKey) =>
        $"Report Aggregator · {ruleKey}";

    public static string CreateReportVerifierAgentKey(StoredProjectPlanTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:report-verifier";

    public static string CreateReportVerifierAgentDisplayName(string ruleKey) =>
        $"Report Verifier · {ruleKey}";
}
