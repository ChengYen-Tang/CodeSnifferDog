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
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;
using ReportToolFactory = CodeSnifferDog.Modules.Tools.Report.ToolFactory;
using ReportToolCallbacks = CodeSnifferDog.Modules.Tools.Report.AggregatorToolCallbacks;
using ReportVerifierToolCallbacks = CodeSnifferDog.Modules.Tools.Report.VerifierToolCallbacks;
using RuleReviewToolFactory = CodeSnifferDog.Modules.Tools.RuleReview.ToolFactory;
using RuleReviewAgentToolCallbacks = CodeSnifferDog.Modules.Tools.RuleReview.AgentToolCallbacks;
using RuleReviewVerifierToolCallbacks = CodeSnifferDog.Modules.Tools.RuleReview.VerifierToolCallbacks;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Tests.Modules.Tools;

[TestClass]
public sealed class ToolFactoryTypedSeamTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void CommonToolFactory_UsesTypedCallbacks()
    {
        IList<AITool> tools = CommonToolFactory.CreateTools(new CommonToolCallbacks(
            RunShellCommandTool,
            RunRipgrepCommandTool,
            ReadFileRangeTool));

        CollectionAssert.AreEqual(new[] { "ReadFileRange", "Ripgrep", "Shell" }, tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task CommonToolFactory_InvokesMatchingTypedCallbacks()
    {
        List<string> invokedCallbacks = [];
        IList<AITool> tools = CommonToolFactory.CreateTools(new CommonToolCallbacks(
            (Command, cancellationToken) =>
            {
                invokedCallbacks.Add($"shell:{Command}");
                return ValueTask.FromResult(Succeeded());
            },
            (Command, cancellationToken) =>
            {
                invokedCallbacks.Add($"ripgrep:{Command}");
                return ValueTask.FromResult(Succeeded());
            },
            (Path, OffsetLine, LimitLines, cancellationToken) =>
            {
                invokedCallbacks.Add($"read:{Path}:{OffsetLine}:{LimitLines}");
                return ValueTask.FromResult(SucceededRead());
            }));

        AIFunction shellFunction = Assert.IsInstanceOfType<AIFunction>(tools.Single(tool => tool.Name == "Shell"));
        AIFunction ripgrepFunction = Assert.IsInstanceOfType<AIFunction>(tools.Single(tool => tool.Name == "Ripgrep"));
        AIFunction readFileRangeFunction = Assert.IsInstanceOfType<AIFunction>(tools.Single(tool => tool.Name == "ReadFileRange"));

        await shellFunction.InvokeAsync(new AIFunctionArguments { ["Command"] = "Get-ChildItem" }, TestContext.CancellationToken);
        await ripgrepFunction.InvokeAsync(new AIFunctionArguments { ["Command"] = "-n \"SystemPrompt\" ." }, TestContext.CancellationToken);
        await readFileRangeFunction.InvokeAsync(new AIFunctionArguments { ["Path"] = "Program.cs", ["OffsetLine"] = 2, ["LimitLines"] = 3 }, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "shell:Get-ChildItem", "ripgrep:-n \"SystemPrompt\" .", "read:Program.cs:2:3" },
            invokedCallbacks.ToArray());
    }

    [TestMethod]
    public async Task CommonToolFactory_InvokesTypedCallbacks_WithoutCaseSensitiveArgumentNames()
    {
        List<string> invokedCallbacks = [];
        IList<AITool> tools = CommonToolFactory.CreateTools(new CommonToolCallbacks(
            (Command, cancellationToken) =>
            {
                invokedCallbacks.Add($"shell:{Command}");
                return ValueTask.FromResult(Succeeded());
            },
            (Command, cancellationToken) =>
            {
                invokedCallbacks.Add($"ripgrep:{Command}");
                return ValueTask.FromResult(Succeeded());
            },
            (Path, OffsetLine, LimitLines, cancellationToken) =>
            {
                invokedCallbacks.Add($"read:{Path}:{OffsetLine}:{LimitLines}");
                return ValueTask.FromResult(SucceededRead());
            }));

        AIFunction shellFunction = Assert.IsInstanceOfType<AIFunction>(tools.Single(tool => tool.Name == "Shell"));
        AIFunction ripgrepFunction = Assert.IsInstanceOfType<AIFunction>(tools.Single(tool => tool.Name == "Ripgrep"));
        AIFunction readFileRangeFunction = Assert.IsInstanceOfType<AIFunction>(tools.Single(tool => tool.Name == "ReadFileRange"));

        await shellFunction.InvokeAsync(new AIFunctionArguments { ["command"] = "Get-ChildItem" }, TestContext.CancellationToken);
        await ripgrepFunction.InvokeAsync(new AIFunctionArguments { ["COMMAND"] = "-n \"SystemPrompt\" ." }, TestContext.CancellationToken);
        await readFileRangeFunction.InvokeAsync(
            new AIFunctionArguments
            {
                ["path"] = "Program.cs",
                ["offsetline"] = 2,
                ["LIMITLINES"] = 3,
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "shell:Get-ChildItem", "ripgrep:-n \"SystemPrompt\" .", "read:Program.cs:2:3" },
            invokedCallbacks.ToArray());
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
        IList<AITool> agentTools = ToolFactory.CreateAgentTools(new ProjectPlanAgentToolCallbacks(
            AddProjectPlanTaskItemTool,
            AddProjectPlanTaskItemsTool,
            DeleteProjectPlanTaskItemTool,
            ListProjectPlanTaskItemsTool));
        IList<AITool> verifierTools = ToolFactory.CreateVerifierTools(new ProjectPlanVerifierToolCallbacks(
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
        IList<AITool> aggregatorTools = ReportToolFactory.CreateAggregatorTools(new ReportToolCallbacks(
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

    private static ValueTask<CommandExecutionResult> ReadFileRangeTool(
        string Path,
        int OffsetLine,
        int LimitLines,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(SucceededRead());

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
        IReadOnlyList<PlanFile> Files,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AddProjectPlanTaskItemResult { ProjectPlanTaskItemId = "task-item" });

    private static ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsTool(
        IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AddProjectPlanTaskItemsResult { ProjectPlanTaskItemIds = [] });

    private static ValueTask<bool> DeleteProjectPlanTaskItemTool(string ProjectPlanTaskItemId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    private static ValueTask<IReadOnlyList<StoredTaskItem>> ListProjectPlanTaskItemsTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<StoredTaskItem>>([]);

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

    private static ValueTask<ReviewStoredIssue> GetRuleReviewIssueTool(string RuleReviewIssueId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ReviewStoredIssue { RuleReviewIssueId = RuleReviewIssueId, IssueType = "", Severity = Severity.Low, FileOrFunction = "", RelevantCodePatternOrExpression = "", WhyThisIsAProblem = "", Confidence = "", FollowUpFiles = "", SuggestedFixDirection = "", ScopeCoverage = "", CrossScopeAnalysis = "", ReviewStrategy = "" });

    private static ValueTask<IReadOnlyList<ReviewStoredIssue>> ListRuleReviewIssuesTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ReviewStoredIssue>>([]);

    private static ValueTask<ReviewStoredIssue> UpdateRuleReviewIssueTool(
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

    private static ValueTask<ReportStoredIssue> GetRuleReportIssueTool(string RuleReportIssueId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ReportStoredIssue { RuleReportIssueId = RuleReportIssueId, IssueType = "", Severity = Severity.Low, FileOrFunction = "", RelevantCodePatternOrExpression = "", WhyThisIsAProblem = "", Confidence = "", FollowUpFiles = "", SuggestedFixDirection = "", ScopeCoverage = "", CrossScopeAnalysis = "", ReviewStrategy = "" });

    private static ValueTask<IReadOnlyList<ReportStoredIssue>> ListRuleReportIssuesTool(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ReportStoredIssue>>([]);

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

    private static ValueTask<ReportStoredIssue> UpdateRuleReportIssueTool(
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

    private static CommandExecutionResult SucceededRead() =>
        new()
        {
            ExitCode = 0,
            StandardOutput = "class C {}",
            StandardError = "",
        };
}
