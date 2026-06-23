using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.Tools;

[TestClass]
public sealed class ToolFactoryTypedSeamTests
{
    [TestMethod]
    public void CommonToolFactory_UsesTypedCallbacks()
    {
        IList<AITool> tools = CommonToolFactory.CreateTools(new CommonToolCallbacks(
            RunShellCommandTool,
            RunRipgrepCommandTool));

        CollectionAssert.AreEqual(new[] { "RunShellCommand", "RunRipgrepCommand" }, tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void ScanToolFactory_UsesTypedCallbacks()
    {
        IList<AITool> agentTools = ScanToolFactory.CreateAgentTools(new ScanAgentToolCallbacks(
            AddScanProjectTool,
            AddScanProjectsTool,
            DeleteScanProjectTool,
            ListScanProjectsTool));
        IList<AITool> verifierTools = ScanToolFactory.CreateVerifierTools(new ScanVerifierToolCallbacks(
            ListScanProjectsTool,
            SubmitReviewVerdictTool));

        CollectionAssert.AreEqual(
            new[] { "AddScanProject", "AddScanProjects", "DeleteScanProject", "ListScanProjects" },
            agentTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "ListScanProjects", "SubmitReviewVerdict" },
            verifierTools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void ProjectPlanToolFactory_UsesTypedCallbacks()
    {
        IList<AITool> agentTools = ProjectPlanToolFactory.CreateAgentTools(new ProjectPlanAgentToolCallbacks(
            AddProjectPlanTaskItemTool,
            AddProjectPlanTaskItemsTool,
            DeleteProjectPlanTaskItemTool,
            ListProjectPlanTaskItemsTool));
        IList<AITool> verifierTools = ProjectPlanToolFactory.CreateVerifierTools(new ProjectPlanVerifierToolCallbacks(
            ListProjectPlanTaskItemsTool,
            SubmitReviewVerdictTool));

        CollectionAssert.AreEqual(
            new[] { "AddProjectPlanTaskItem", "AddProjectPlanTaskItems", "DeleteProjectPlanTaskItem", "ListProjectPlanTaskItems" },
            agentTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "ListProjectPlanTaskItems", "SubmitReviewVerdict" },
            verifierTools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void RuleReviewToolFactory_UsesTypedCallbacks()
    {
        IList<AITool> agentTools = RuleReviewToolFactory.CreateAgentTools(new RuleReviewAgentToolCallbacks(
            CreateRuleReviewIssueTool,
            GetRuleReviewIssueTool,
            ListRuleReviewIssuesTool,
            UpdateRuleReviewIssueTool,
            DeleteRuleReviewIssueTool,
            SubmitNoIssueConclusionTool));
        IList<AITool> verifierTools = RuleReviewToolFactory.CreateVerifierTools(
            new RuleReviewVerifierToolCallbacks(SubmitReviewVerdictTool));

        CollectionAssert.AreEqual(
            new[] { "CreateRuleReviewIssue", "GetRuleReviewIssue", "ListRuleReviewIssues", "UpdateRuleReviewIssue", "DeleteRuleReviewIssue", "SubmitNoIssueConclusion" },
            agentTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "SubmitReviewVerdict" }, verifierTools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void ReportToolFactory_UsesTypedCallbacks()
    {
        IList<AITool> aggregatorTools = ReportToolFactory.CreateAggregatorTools(new ReportAggregatorToolCallbacks(
            GetRuleReportIssueTool,
            ListRuleReportIssuesTool,
            CreateRuleReportIssueTool,
            UpdateRuleReportIssueTool,
            DeleteRuleReportIssueTool));
        IList<AITool> verifierTools = ReportToolFactory.CreateVerifierTools(
            new ReportVerifierToolCallbacks(SubmitReviewVerdictTool));

        CollectionAssert.AreEqual(
            new[] { "GetRuleReportIssue", "ListRuleReportIssues", "CreateRuleReportIssue", "UpdateRuleReportIssue", "DeleteRuleReportIssue" },
            aggregatorTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "SubmitReviewVerdict" }, verifierTools.Select(tool => tool.Name).ToArray());
    }

    private static ValueTask<CommandExecutionResult> RunShellCommandTool(string Command, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeeded());

    private static ValueTask<CommandExecutionResult> RunRipgrepCommandTool(string Command, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeeded());

    private static ValueTask<AddScanProjectResult> AddScanProjectTool(
        string ProjectName,
        string ProjectPath,
        string ProjectType,
        string Reason,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AddScanProjectResult { ScanProjectId = "scan-project" });

    private static ValueTask<AddScanProjectsResult> AddScanProjectsTool(
        IReadOnlyList<AddScanProjectArgs> Projects,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AddScanProjectsResult { ScanProjectIds = [] });

    private static ValueTask<bool> DeleteScanProjectTool(string ScanProjectId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<StoredScanProject>>([]);

    private static ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemTool(
        IReadOnlyList<ProjectPlanFile> Files,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AddProjectPlanTaskItemResult { ProjectPlanTaskItemId = "task-item" });

    private static ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsTool(
        IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AddProjectPlanTaskItemsResult { ProjectPlanTaskItemIds = [] });

    private static ValueTask<bool> DeleteProjectPlanTaskItemTool(string ProjectPlanTaskItemId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListProjectPlanTaskItemsTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<StoredProjectPlanTaskItem>>([]);

    private static ValueTask<CreateRuleReviewIssueResult> CreateRuleReviewIssueTool(
        string IssueType,
        string Severity,
        string FileOrFunction,
        string RelevantCodePatternOrExpression,
        string WhyThisIsAProblem,
        string Confidence,
        string FollowUpFiles,
        string SuggestedFixDirection,
        string ScopeCoverage,
        string CrossScopeAnalysis,
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new CreateRuleReviewIssueResult { RuleReviewIssueId = "review-issue" });

    private static ValueTask<StoredRuleReviewIssue> GetRuleReviewIssueTool(string RuleReviewIssueId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new StoredRuleReviewIssue { RuleReviewIssueId = RuleReviewIssueId, IssueType = "", Severity = RuleReviewSeverity.Low, FileOrFunction = "", RelevantCodePatternOrExpression = "", WhyThisIsAProblem = "", Confidence = "", FollowUpFiles = "", SuggestedFixDirection = "", ScopeCoverage = "", CrossScopeAnalysis = "", ReviewStrategy = "" });

    private static ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListRuleReviewIssuesTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<StoredRuleReviewIssue>>([]);

    private static ValueTask<StoredRuleReviewIssue> UpdateRuleReviewIssueTool(
        string RuleReviewIssueId,
        string IssueType,
        string Severity,
        string FileOrFunction,
        string RelevantCodePatternOrExpression,
        string WhyThisIsAProblem,
        string Confidence,
        string FollowUpFiles,
        string SuggestedFixDirection,
        string ScopeCoverage,
        string CrossScopeAnalysis,
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        GetRuleReviewIssueTool(RuleReviewIssueId, cancellationToken);

    private static ValueTask<bool> DeleteRuleReviewIssueTool(string RuleReviewIssueId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static ValueTask<bool> SubmitNoIssueConclusionTool(
        string ReviewStrategy,
        string ScopeCoverage,
        string CrossScopeAnalysis,
        string WhyNoIssueWasFound,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static ValueTask<StoredRuleReportIssue> GetRuleReportIssueTool(string RuleReportIssueId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new StoredRuleReportIssue { RuleReportIssueId = RuleReportIssueId, IssueType = "", Severity = RuleReviewSeverity.Low, FileOrFunction = "", RelevantCodePatternOrExpression = "", WhyThisIsAProblem = "", Confidence = "", FollowUpFiles = "", SuggestedFixDirection = "", ScopeCoverage = "", CrossScopeAnalysis = "", ReviewStrategy = "" });

    private static ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListRuleReportIssuesTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<StoredRuleReportIssue>>([]);

    private static ValueTask<CreateRuleReportIssueResult> CreateRuleReportIssueTool(
        string IssueType,
        string Severity,
        string FileOrFunction,
        string RelevantCodePatternOrExpression,
        string WhyThisIsAProblem,
        string Confidence,
        string FollowUpFiles,
        string SuggestedFixDirection,
        string ScopeCoverage,
        string CrossScopeAnalysis,
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new CreateRuleReportIssueResult { RuleReportIssueId = "report-issue" });

    private static ValueTask<StoredRuleReportIssue> UpdateRuleReportIssueTool(
        string RuleReportIssueId,
        string IssueType,
        string Severity,
        string FileOrFunction,
        string RelevantCodePatternOrExpression,
        string WhyThisIsAProblem,
        string Confidence,
        string FollowUpFiles,
        string SuggestedFixDirection,
        string ScopeCoverage,
        string CrossScopeAnalysis,
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        GetRuleReportIssueTool(RuleReportIssueId, cancellationToken);

    private static ValueTask<bool> DeleteRuleReportIssueTool(string RuleReportIssueId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static ValueTask<bool> SubmitReviewVerdictTool(bool Approved, string Message, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static CommandExecutionResult Succeeded() =>
        new()
        {
            ExitCode = 0,
            StandardOutput = "",
            StandardError = "",
        };
}
