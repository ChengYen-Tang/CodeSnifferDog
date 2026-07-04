using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

/// <summary>
/// Provides stable status names, group keys, agent keys, and display names for the review-agent runtime.
/// </summary>
public static class AgentStatusCatalog
{
    /// <summary>
    /// Status used before an agent starts running.
    /// </summary>
    public const string WaitingStatus = "Waiting";

    /// <summary>
    /// Status used while an agent is actively running.
    /// </summary>
    public const string RunningStatus = "Running";

    /// <summary>
    /// Status used when an agent completed successfully.
    /// </summary>
    public const string CompletedStatus = "Completed";

    /// <summary>
    /// Status used when an agent or workflow completed in a degraded state.
    /// </summary>
    public const string DegradedStatus = "Degraded";

    /// <summary>
    /// Creates the stable group key for repository scan agents.
    /// </summary>
    /// <returns>The scan group key.</returns>
    public static string CreateScanGroupKey() => "scan";

    /// <summary>
    /// Creates the display name for the repository scan group.
    /// </summary>
    /// <returns>The scan group display name.</returns>
    public static string CreateScanGroupDisplayName() => "Repository Scan";

    /// <summary>
    /// Creates the stable key for the scan agent.
    /// </summary>
    /// <returns>The scan agent key.</returns>
    public static string CreateScanAgentKey() => "scan:scan";

    /// <summary>
    /// Creates the display name for the scan agent.
    /// </summary>
    /// <returns>The scan agent display name.</returns>
    public static string CreateScanAgentDisplayName() => "Scan";

    /// <summary>
    /// Creates the stable key for the scan verifier agent.
    /// </summary>
    /// <returns>The scan verifier agent key.</returns>
    public static string CreateScanVerifierAgentKey() => "scan:scan-verifier";

    /// <summary>
    /// Creates the display name for the scan verifier agent.
    /// </summary>
    /// <returns>The scan verifier agent display name.</returns>
    public static string CreateScanVerifierAgentDisplayName() => "Scan Verifier";

    /// <summary>
    /// Creates the stable group key for one scanned project's planning agents.
    /// </summary>
    /// <param name="scanProject">Scanned project that owns the planning group.</param>
    /// <returns>The project-plan group key.</returns>
    public static string CreateProjectPlanGroupKey(StoredScanProject scanProject) =>
        $"project-plan:{scanProject.ScanProjectId}";

    /// <summary>
    /// Creates the display name for one scanned project's planning group.
    /// </summary>
    /// <param name="scanProject">Scanned project that owns the planning group.</param>
    /// <returns>The project-plan group display name.</returns>
    public static string CreateProjectPlanGroupDisplayName(StoredScanProject scanProject) =>
        $"Project Plan: {scanProject.ProjectName}";

    /// <summary>
    /// Creates the stable key for one project's planner agent.
    /// </summary>
    /// <param name="scanProject">Scanned project that owns the planner agent.</param>
    /// <returns>The planner agent key.</returns>
    public static string CreateProjectPlannerAgentKey(StoredScanProject scanProject) =>
        $"project-plan:{scanProject.ScanProjectId}:planner";

    /// <summary>
    /// Creates the display name for the project planner agent.
    /// </summary>
    /// <returns>The planner agent display name.</returns>
    public static string CreateProjectPlannerAgentDisplayName() => "Project Plan";

    /// <summary>
    /// Creates the stable key for one project's verifier agent.
    /// </summary>
    /// <param name="scanProject">Scanned project that owns the verifier agent.</param>
    /// <returns>The verifier agent key.</returns>
    public static string CreateProjectVerifierAgentKey(StoredScanProject scanProject) =>
        $"project-plan:{scanProject.ScanProjectId}:verifier";

    /// <summary>
    /// Creates the display name for the project verifier agent.
    /// </summary>
    /// <returns>The verifier agent display name.</returns>
    public static string CreateProjectVerifierAgentDisplayName() => "Project Verifier";

    /// <summary>
    /// Creates the stable group key for one review task item.
    /// </summary>
    /// <param name="taskItem">Task item that owns the review group.</param>
    /// <returns>The review-task group key.</returns>
    public static string CreateReviewTaskGroupKey(StoredTaskItem taskItem) =>
        $"review-task:{taskItem.ProjectPlanTaskItemId}";

    /// <summary>
    /// Creates the display name for one review task group.
    /// </summary>
    /// <param name="reviewNumber">Human-readable review sequence number.</param>
    /// <returns>The review-task group display name.</returns>
    public static string CreateReviewTaskGroupDisplayName(int reviewNumber) =>
        $"Review: {reviewNumber}";

    /// <summary>
    /// Creates the stable key for one rule-review agent.
    /// </summary>
    /// <param name="taskItem">Task item that owns the review agent.</param>
    /// <param name="ruleKey">Rule key reviewed by the agent.</param>
    /// <returns>The rule-review agent key.</returns>
    public static string CreateRuleReviewAgentKey(StoredTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:rule-review";

    /// <summary>
    /// Creates the display name for one rule-review agent.
    /// </summary>
    /// <param name="ruleKey">Rule key reviewed by the agent.</param>
    /// <returns>The rule-review agent display name.</returns>
    public static string CreateRuleReviewAgentDisplayName(string ruleKey) =>
        $"Rule Review · {ruleKey}";

    /// <summary>
    /// Creates the stable key for one review verifier agent.
    /// </summary>
    /// <param name="taskItem">Task item that owns the verifier agent.</param>
    /// <param name="ruleKey">Rule key reviewed by the verifier.</param>
    /// <returns>The review verifier agent key.</returns>
    public static string CreateReviewVerifierAgentKey(StoredTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:review-verifier";

    /// <summary>
    /// Creates the display name for one review verifier agent.
    /// </summary>
    /// <param name="ruleKey">Rule key reviewed by the verifier.</param>
    /// <returns>The review verifier agent display name.</returns>
    public static string CreateReviewVerifierAgentDisplayName(string ruleKey) =>
        $"Review Verifier · {ruleKey}";

    /// <summary>
    /// Creates the stable key for one report aggregator agent.
    /// </summary>
    /// <param name="taskItem">Task item that owns the report aggregator.</param>
    /// <param name="ruleKey">Rule key whose report is being aggregated.</param>
    /// <returns>The report aggregator agent key.</returns>
    public static string CreateReportAggregatorAgentKey(StoredTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:report-aggregator";

    /// <summary>
    /// Creates the display name for one report aggregator agent.
    /// </summary>
    /// <param name="ruleKey">Rule key whose report is being aggregated.</param>
    /// <returns>The report aggregator agent display name.</returns>
    public static string CreateReportAggregatorAgentDisplayName(string ruleKey) =>
        $"Report Aggregator · {ruleKey}";

    /// <summary>
    /// Creates the stable key for one report verifier agent.
    /// </summary>
    /// <param name="taskItem">Task item that owns the report verifier.</param>
    /// <param name="ruleKey">Rule key whose report is being verified.</param>
    /// <returns>The report verifier agent key.</returns>
    public static string CreateReportVerifierAgentKey(StoredTaskItem taskItem, string ruleKey) =>
        $"{CreateReviewTaskGroupKey(taskItem)}:{ruleKey}:report-verifier";

    /// <summary>
    /// Creates the display name for one report verifier agent.
    /// </summary>
    /// <param name="ruleKey">Rule key whose report is being verified.</param>
    /// <returns>The report verifier agent display name.</returns>
    public static string CreateReportVerifierAgentDisplayName(string ruleKey) =>
        $"Report Verifier · {ruleKey}";
}
