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
    public async Task RunAsync_RunsRuleFlowWorkflow_ForEachRule_AndPreservesRuleOrder()
    {
        List<string> executedRules = [];
        ReviewGroupWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                executedRules.Add(ruleMarkdown);
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedWithReport)));
            });

        Result<ReviewGroupWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreateTaskItem(),
            ["- Rule A", "- Rule B", "- Rule C"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { "- Rule A", "- Rule B", "- Rule C" }, executedRules);
        CollectionAssert.AreEqual(new[] { "- Rule A", "- Rule B", "- Rule C" }, result.Value.RuleMarkdowns.ToArray());
        CollectionAssert.AreEqual(new[] { "- Rule A", "- Rule B", "- Rule C" }, result.Value.FlowResults.Select(flow => flow.RuleMarkdown).ToArray());
        Assert.IsTrue(result.Value.HasAnyRuleFlows);
        Assert.IsTrue(result.Value.AllRuleFlowsFinished);
        Assert.AreEqual(3, result.Value.ApprovedCompletionCount);
        Assert.AreEqual(0, result.Value.DegradedCompletionCount);
    }

    [TestMethod]
    public async Task RunAsync_RespectsParallelLimit_WhileAllowingParallelExecution()
    {
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        ReviewGroupWorkflow workflow = new(
            async (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                int newConcurrency = Interlocked.Increment(ref currentConcurrency);
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, newConcurrency);

                try
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedWithReport));
                }
                finally
                {
                    Interlocked.Decrement(ref currentConcurrency);
                }
            },
            new ReviewGroupWorkflowOptions
            {
                MaxConcurrentRuleFlows = 2,
            });

        Result<ReviewGroupWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreateTaskItem(),
            ["- Rule A", "- Rule B", "- Rule C", "- Rule D"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, maxObservedConcurrency);
    }

    [TestMethod]
    public async Task RunAsync_CountsApprovedAndDegradedCompletions()
    {
        ReviewGroupWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                RuleFlowCompletionState completionState = ruleMarkdown switch
                {
                    "- Rule A" => RuleFlowCompletionState.ApprovedWithReport,
                    "- Rule B" => RuleFlowCompletionState.DegradedNoIssue,
                    _ => RuleFlowCompletionState.DegradedWithReport,
                };

                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, completionState)));
            });

        Result<ReviewGroupWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreateTaskItem(),
            ["- Rule A", "- Rule B", "- Rule C"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, result.Value.ApprovedCompletionCount);
        Assert.AreEqual(2, result.Value.DegradedCompletionCount);
    }

    [TestMethod]
    public async Task RunAsync_SucceedsWithEmptyRuleList()
    {
        bool ruleFlowCalled = false;
        ReviewGroupWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                ruleFlowCalled = true;
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedWithReport)));
            });

        Result<ReviewGroupWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreateTaskItem(),
            []);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(ruleFlowCalled);
        Assert.IsFalse(result.Value.HasAnyRuleFlows);
        Assert.IsTrue(result.Value.AllRuleFlowsFinished);
        Assert.IsEmpty(result.Value.FlowResults);
    }

    [TestMethod]
    public async Task RunAsync_FailsWhenAnyRuleFlowFails()
    {
        ReviewGroupWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                if (ruleMarkdown == "- Rule B")
                    return Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("Rule B failed."));

                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedWithReport)));
            });

        Result<ReviewGroupWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreateTaskItem(),
            ["- Rule A", "- Rule B"]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("Rule B failed.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_FailsWhenParallelLimitIsInvalid()
    {
        ReviewGroupWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedWithReport))),
            new ReviewGroupWorkflowOptions
            {
                MaxConcurrentRuleFlows = 0,
            });

        Result<ReviewGroupWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreateTaskItem(),
            ["- Rule A"]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("MaxConcurrentRuleFlows", StringComparison.Ordinal)));
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
