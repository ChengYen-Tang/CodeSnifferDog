using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Workflows.ReviewGroup;
using FluentResults;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using ReviewGroupWorkflowResult = CodeSnifferDog.Models.ReviewGroup.WorkflowResult;
using ReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Tests.Workflows.ReviewGroup;

[TestClass]
public sealed class WorkflowTests
{
    private static readonly string[] RuleKeys =
    [
        "rule-a",
        "rule-b",
        "rule-c",
    ];

    [TestMethod]
    public void Run_PreservesRuleOrder_AndCountsApprovedAndDegradedCompletions()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        Result<ReviewGroupWorkflowResult> result = Workflow.Run(
            taskItem,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B"), ("rule-c", "- Rule C")),
            [
                CreateRuleFlowResult(taskItem, "rule-a", "- Rule A", CompletionState.ApprovedWithReport),
                CreateRuleFlowResult(taskItem, "rule-b", "- Rule B", CompletionState.DegradedNoIssue),
                CreateRuleFlowResult(taskItem, "rule-c", "- Rule C", CompletionState.DegradedWithReport),
            ]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(RuleKeys, result.Value.FlowResults.Select(flow => flow.ReviewResult.RuleKey).ToArray());
        Assert.AreEqual(1, result.Value.FlowResults.Count(flow => flow.IsApprovedCompletion));
        Assert.AreEqual(2, result.Value.FlowResults.Count(flow => flow.IsDegradedCompletion));
    }

    [TestMethod]
    public void Run_SucceedsWithEmptyRuleList()
    {
        Result<ReviewGroupWorkflowResult> result = Workflow.Run(CreateTaskItem(), [], []);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsEmpty(result.Value.FlowResults);
    }

    [TestMethod]
    public void Run_FailsWhenFlowResultCountDoesNotMatchRuleCount()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        Result<ReviewGroupWorkflowResult> result = Workflow.Run(
            taskItem,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            [CreateRuleFlowResult(taskItem, "rule-a", "- Rule A", CompletionState.ApprovedWithReport)]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("count", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Run_FailsWhenRuleOrderDoesNotMatchFlowOrder()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        Result<ReviewGroupWorkflowResult> result = Workflow.Run(
            taskItem,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            [
                CreateRuleFlowResult(taskItem, "rule-b", "- Rule B", CompletionState.ApprovedWithReport),
                CreateRuleFlowResult(taskItem, "rule-a", "- Rule A", CompletionState.ApprovedWithReport),
            ]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("order", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Run_FailsWhenFlowTaskItemDoesNotMatchReviewGroupTaskItem()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        StoredTaskItem mismatchedTaskItem = new()
        {
            ProjectPlanTaskItemId = "task-item-2",
            Files = taskItem.Files,
        };
        Result<ReviewGroupWorkflowResult> result = Workflow.Run(
            taskItem,
            CreateRuleDefinitions(("rule-a", "- Rule A")),
            [CreateRuleFlowResult(mismatchedTaskItem, "rule-a", "- Rule A", CompletionState.ApprovedWithReport)]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("task item", StringComparison.OrdinalIgnoreCase)));
    }

    private static StoredTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-item-1",
            Files =
            [
                new PlanFile
                {
                    FilePath = "src/Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static RuleFlowWorkflowResult CreateRuleFlowResult(
        StoredTaskItem taskItem,
        string ruleKey,
        string _,
        CompletionState completionState)
    {
        bool approved = completionState is CompletionState.ApprovedNoIssue or CompletionState.ApprovedWithReport;
        bool enteredReportAggregation = completionState is CompletionState.ApprovedWithReport or CompletionState.DegradedWithReport;
        bool hasNoIssue = completionState is CompletionState.ApprovedNoIssue or CompletionState.DegradedNoIssue;
        ReviewStoredIssue[] reviewIssues = enteredReportAggregation ? [CreateReviewIssue()] : [];

        return new RuleFlowWorkflowResult
        {
            ReviewResult = new RuleReviewWorkflowResult
            {
                TaskItem = taskItem,
                RuleKey = ruleKey,
                Issues = reviewIssues,
                NoIssueConclusion = hasNoIssue ? CreateNoIssueConclusion() : null,
                Verdict = new ReviewVerdict
                {
                    Approved = approved,
                    Message = approved ? "Review accepted." : "Review degraded.",
                },
                ContinuedAfterVerifierRejectionLimit = !approved && !hasNoIssue && enteredReportAggregation,
                StoppedAfterMissingSubmissionLimit = completionState == CompletionState.DegradedMissingSubmission,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            },
            ReportResult = enteredReportAggregation
                ? new ReportWorkflowResult
                {
                    RuleKey = ruleKey,
                    TaskItem = taskItem,
                    Diff = new Diff
                    {
                        CreatedIssues = [CreateReportIssue()],
                        UpdatedIssues = [],
                        DeletedIssues = [],
                    },
                    RepositoryIssues = [CreateReportIssue()],
                    Verdict = new ReviewVerdict
                    {
                        Approved = approved,
                        Message = approved ? "Report accepted." : "Report degraded.",
                    },
                    ContinuedAfterVerifierRejectionLimit = !approved,
                    AggregatorAttempts = 1,
                    VerifierAttempts = 1,
                }
                : null,
            CompletionState = completionState,
        };
    }

    private static RuleDefinition[] CreateRuleDefinitions(params (string RuleKey, string RuleMarkdown)[] definitions) =>
        [.. definitions.Select(definition => new RuleDefinition
        {
            RuleKey = definition.RuleKey,
            RuleMarkdown = definition.RuleMarkdown,
        })];

    private static ReviewStoredIssue CreateReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the request path.",
            Confidence = "High",
            FollowUpFiles = "Service.cs",
            SuggestedFixDirection = "Use the cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "Followed the call into Service.cs.",
        };

    private static ReportStoredIssue CreateReportIssue() =>
        new()
        {
            RuleReportIssueId = "report-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the request path.",
            Confidence = "High",
            FollowUpFiles = "Service.cs",
            SuggestedFixDirection = "Use the cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "Followed the call into Service.cs.",
        };

    private static NoIssueConclusion CreateNoIssueConclusion() =>
        new()
        {
            ReviewStrategy = "Reviewed the entry point and the immediate dependency.",
            ScopeCoverage = "Inspected Program.cs and Service.cs.",
            CrossScopeAnalysis = "No further dependency tracing was required.",
            WhyNoIssueWasFound = "No issue matching the rule was found.",
        };
}
