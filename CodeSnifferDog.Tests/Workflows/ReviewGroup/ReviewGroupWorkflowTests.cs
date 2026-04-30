using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
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
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B"), ("rule-c", "- Rule C")),
            [
                CreateRuleFlowResult(taskItem, "rule-a", "- Rule A", RuleFlowCompletionState.ApprovedWithReport),
                CreateRuleFlowResult(taskItem, "rule-b", "- Rule B", RuleFlowCompletionState.DegradedNoIssue),
                CreateRuleFlowResult(taskItem, "rule-c", "- Rule C", RuleFlowCompletionState.DegradedWithReport),
            ]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { "rule-a", "rule-b", "rule-c" }, result.Value.RuleKeys.ToArray());
        CollectionAssert.AreEqual(new[] { "rule-a", "rule-b", "rule-c" }, result.Value.FlowResults.Select(flow => flow.RuleKey).ToArray());
        Assert.IsTrue(result.Value.HasAnyRuleFlows);
        Assert.IsTrue(result.Value.AllRuleFlowsFinished);
        Assert.AreEqual(1, result.Value.ApprovedCompletionCount);
        Assert.AreEqual(2, result.Value.DegradedCompletionCount);
    }

    [TestMethod]
    public void Run_SucceedsWithEmptyRuleList()
    {
        ReviewGroupWorkflow workflow = new();
        Result<ReviewGroupWorkflowResult> result = workflow.Run(CreateTaskItem(), Array.Empty<ReviewAgentRuleDefinition>(), []);

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
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            [CreateRuleFlowResult(taskItem, "rule-a", "- Rule A", RuleFlowCompletionState.ApprovedWithReport)]);

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
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            [
                CreateRuleFlowResult(taskItem, "rule-b", "- Rule B", RuleFlowCompletionState.ApprovedWithReport),
                CreateRuleFlowResult(taskItem, "rule-a", "- Rule A", RuleFlowCompletionState.ApprovedWithReport),
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
            CreateRuleDefinitions(("rule-a", "- Rule A")),
            [CreateRuleFlowResult(mismatchedTaskItem, "rule-a", "- Rule A", RuleFlowCompletionState.ApprovedWithReport)]);

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
        string ruleKey,
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
            RuleKey = ruleKey,
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
                    RuleKey = ruleKey,
                    TaskItem = taskItem,
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

    private static IReadOnlyList<ReviewAgentRuleDefinition> CreateRuleDefinitions(params (string RuleKey, string RuleMarkdown)[] definitions) =>
        definitions.Select(definition => new ReviewAgentRuleDefinition
        {
            RuleKey = definition.RuleKey,
            RuleMarkdown = definition.RuleMarkdown,
        }).ToArray();

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
