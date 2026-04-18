using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Workflows.ReviewGroup;
using FluentResults;

namespace CodeSnifferDog.Tests.Workflows.ReviewGroup;

[TestClass]
public sealed class ReviewGroupWorkflowTests
{
    [TestMethod]
    public void Run_PreservesRuleOrder_AndCountsApprovedAndDegradedCompletions()
    {
        ReviewGroupWorkflow workflow = new();
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        Result<ReviewGroupWorkflowResult> result = workflow.Run(
            taskItem,
            ["- Rule A", "- Rule B", "- Rule C"],
            [
                CreateRuleFlowResult(taskItem, "- Rule A", RuleFlowCompletionState.ApprovedWithReport),
                CreateRuleFlowResult(taskItem, "- Rule B", RuleFlowCompletionState.DegradedNoIssue),
                CreateRuleFlowResult(taskItem, "- Rule C", RuleFlowCompletionState.DegradedWithReport),
            ]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { "- Rule A", "- Rule B", "- Rule C" }, result.Value.RuleMarkdowns.ToArray());
        CollectionAssert.AreEqual(new[] { "- Rule A", "- Rule B", "- Rule C" }, result.Value.FlowResults.Select(flow => flow.RuleMarkdown).ToArray());
        Assert.IsTrue(result.Value.HasAnyRuleFlows);
        Assert.IsTrue(result.Value.AllRuleFlowsFinished);
        Assert.AreEqual(1, result.Value.ApprovedCompletionCount);
        Assert.AreEqual(2, result.Value.DegradedCompletionCount);
    }

    [TestMethod]
    public void Run_SucceedsWithEmptyRuleList()
    {
        ReviewGroupWorkflow workflow = new();
        Result<ReviewGroupWorkflowResult> result = workflow.Run(CreateTaskItem(), [], []);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(result.Value.HasAnyRuleFlows);
        Assert.IsTrue(result.Value.AllRuleFlowsFinished);
        Assert.IsEmpty(result.Value.FlowResults);
    }

    [TestMethod]
    public void Run_FailsWhenFlowResultCountDoesNotMatchRuleCount()
    {
        ReviewGroupWorkflow workflow = new();
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        Result<ReviewGroupWorkflowResult> result = workflow.Run(
            taskItem,
            ["- Rule A", "- Rule B"],
            [CreateRuleFlowResult(taskItem, "- Rule A", RuleFlowCompletionState.ApprovedWithReport)]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("count", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Run_FailsWhenRuleOrderDoesNotMatchFlowOrder()
    {
        ReviewGroupWorkflow workflow = new();
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        Result<ReviewGroupWorkflowResult> result = workflow.Run(
            taskItem,
            ["- Rule A", "- Rule B"],
            [
                CreateRuleFlowResult(taskItem, "- Rule B", RuleFlowCompletionState.ApprovedWithReport),
                CreateRuleFlowResult(taskItem, "- Rule A", RuleFlowCompletionState.ApprovedWithReport),
            ]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("order", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Run_FailsWhenFlowTaskItemDoesNotMatchReviewGroupTaskItem()
    {
        ReviewGroupWorkflow workflow = new();
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        StoredProjectPlanTaskItem mismatchedTaskItem = new()
        {
            ProjectPlanTaskItemId = "task-item-2",
            Files = taskItem.Files,
        };
        Result<ReviewGroupWorkflowResult> result = workflow.Run(
            taskItem,
            ["- Rule A"],
            [CreateRuleFlowResult(mismatchedTaskItem, "- Rule A", RuleFlowCompletionState.ApprovedWithReport)]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("task item", StringComparison.OrdinalIgnoreCase)));
    }

    private static StoredProjectPlanTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-item-1",
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = "src/Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static RuleFlowWorkflowResult CreateRuleFlowResult(
        StoredProjectPlanTaskItem taskItem,
        string ruleMarkdown,
        RuleFlowCompletionState completionState)
    {
        bool approved = completionState is RuleFlowCompletionState.ApprovedNoIssue or RuleFlowCompletionState.ApprovedWithReport;
        bool enteredReportAggregation = completionState is RuleFlowCompletionState.ApprovedWithReport or RuleFlowCompletionState.DegradedWithReport;
        bool hasNoIssue = completionState is RuleFlowCompletionState.ApprovedNoIssue or RuleFlowCompletionState.DegradedNoIssue;
        StoredRuleReviewIssue[] reviewIssues = enteredReportAggregation ? [CreateReviewIssue()] : [];

        return new RuleFlowWorkflowResult
        {
            TaskItem = taskItem,
            RuleMarkdown = ruleMarkdown,
            ReviewResult = new RuleReviewWorkflowResult
            {
                TaskItem = taskItem,
                RuleMarkdown = ruleMarkdown,
                Issues = reviewIssues,
                NoIssueConclusion = hasNoIssue ? CreateNoIssueConclusion() : null,
                Verdict = new ReviewVerdict
                {
                    Approved = approved,
                    Message = approved ? "Review accepted." : "Review degraded.",
                },
                ReviewVerifierApproved = approved,
                ContinuedAfterVerifierRejectionLimit = !approved && !hasNoIssue && enteredReportAggregation,
                StoppedAfterMissingSubmissionLimit = completionState == RuleFlowCompletionState.DegradedMissingSubmission,
                ShouldEnterReportAggregation = enteredReportAggregation,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            },
            ReportResult = enteredReportAggregation
                ? new RuleReportWorkflowResult
                {
                    TaskItem = taskItem,
                    RuleMarkdown = ruleMarkdown,
                    CurrentFlowIssues = reviewIssues,
                    Diff = new RuleReportDiff
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
                    ReportVerifierApproved = approved,
                    ContinuedAfterVerifierRejectionLimit = !approved,
                    AggregatorAttempts = 1,
                    VerifierAttempts = 1,
                }
                : null,
            EnteredReportAggregation = enteredReportAggregation,
            CompletionState = completionState,
        };
    }

    private static StoredRuleReviewIssue CreateReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue-1",
            IssueType = "Performance",
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

    private static StoredRuleReportIssue CreateReportIssue() =>
        new()
        {
            RuleReportIssueId = "report-issue-1",
            IssueType = "Performance",
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
