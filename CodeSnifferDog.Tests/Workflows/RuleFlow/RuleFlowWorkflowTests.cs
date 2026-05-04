using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Workflows.RuleFlow;
using FluentResults;

namespace CodeSnifferDog.Tests.Workflows.RuleFlow;

[TestClass]
public sealed class RuleFlowWorkflowTests
{
    private const string RuleFileName = "performance";
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_CompletesApprovedNoIssueFlow_WithoutEnteringReportAggregation()
    {
        bool reportWorkflowCalled = false;
        RuleFlowWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReviewResult(taskItem, ruleMarkdown, issues: [], noIssueConclusion: CreateNoIssueConclusion(), reviewVerifierApproved: true, shouldEnterReportAggregation: false))),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, issues, cancellationToken) =>
            {
                reportWorkflowCalled = true;
                return Task.FromResult(Result.Ok(CreateReportResult(taskItem, ruleFileName, ruleMarkdown, issues, reportVerifierApproved: true)));
            });

        Result<RuleFlowWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(reportWorkflowCalled);
        Assert.IsNull(result.Value.ReportResult);
        Assert.AreEqual(RuleFlowCompletionState.ApprovedNoIssue, result.Value.CompletionState);
        Assert.IsTrue(result.Value.IsApprovedCompletion);
        Assert.IsNull(result.Value.ReportResult);
    }

    [TestMethod]
    public async Task RunAsync_CompletesApprovedReportFlow_WhenBothStagesAreApproved()
    {
        bool reportWorkflowCalled = false;
        StoredRuleReviewIssue[] issues = [CreateIssue()];
        RuleFlowWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReviewResult(taskItem, ruleMarkdown, issues, noIssueConclusion: null, reviewVerifierApproved: true, shouldEnterReportAggregation: true))),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken) =>
            {
                reportWorkflowCalled = true;
                return Task.FromResult(Result.Ok(CreateReportResult(taskItem, ruleFileName, ruleMarkdown, currentFlowIssues, reportVerifierApproved: true)));
            });

        Result<RuleFlowWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(reportWorkflowCalled);
        Assert.IsNotNull(result.Value.ReportResult);
        Assert.AreEqual(RuleFlowCompletionState.ApprovedWithReport, result.Value.CompletionState);
        Assert.IsTrue(result.Value.IsApprovedCompletion);
        Assert.IsNotNull(result.Value.ReportResult);
        Assert.IsTrue(result.Value.ReportResult.Verdict.Approved);
    }

    [TestMethod]
    public async Task RunAsync_CompletesDegradedReportFlow_WhenReviewStageAlreadyDegraded()
    {
        StoredRuleReviewIssue[] issues = [CreateIssue()];
        RuleFlowWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReviewResult(taskItem, ruleMarkdown, issues, noIssueConclusion: null, reviewVerifierApproved: false, shouldEnterReportAggregation: true, continuedAfterVerifierRejectionLimit: true))),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReportResult(taskItem, ruleFileName, ruleMarkdown, currentFlowIssues, reportVerifierApproved: true))));

        Result<RuleFlowWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsNotNull(result.Value.ReportResult);
        Assert.AreEqual(RuleFlowCompletionState.DegradedWithReport, result.Value.CompletionState);
        Assert.IsTrue(result.Value.IsDegradedCompletion);
        Assert.IsNotNull(result.Value.ReportResult);
        Assert.IsTrue(result.Value.ReportResult.Verdict.Approved);
    }

    [TestMethod]
    public async Task RunAsync_CompletesDegradedMissingSubmissionFlow_WithoutEnteringReportAggregation()
    {
        bool reportWorkflowCalled = false;
        RuleFlowWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReviewResult(taskItem, ruleMarkdown, issues: [], noIssueConclusion: null, reviewVerifierApproved: false, shouldEnterReportAggregation: false, stoppedAfterMissingSubmissionLimit: true))),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, issues, cancellationToken) =>
            {
                reportWorkflowCalled = true;
                return Task.FromResult(Result.Ok(CreateReportResult(taskItem, ruleFileName, ruleMarkdown, issues, reportVerifierApproved: true)));
            });

        Result<RuleFlowWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(reportWorkflowCalled);
        Assert.IsNull(result.Value.ReportResult);
        Assert.AreEqual(RuleFlowCompletionState.DegradedMissingSubmission, result.Value.CompletionState);
        Assert.IsNull(result.Value.ReportResult);
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenReportWorkflowFails()
    {
        RuleFlowWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReviewResult(taskItem, ruleMarkdown, [CreateIssue()], noIssueConclusion: null, reviewVerifierApproved: true, shouldEnterReportAggregation: true))),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, issues, cancellationToken) =>
                Task.FromResult(Result.Fail<RuleReportWorkflowResult>("Report aggregation failed.")));

        Result<RuleFlowWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("Report aggregation failed.", StringComparison.Ordinal)));
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

    private static StoredRuleReviewIssue CreateIssue() =>
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

    private static NoIssueConclusion CreateNoIssueConclusion() =>
        new()
        {
            ReviewStrategy = "Reviewed the entry point and the immediate dependency.",
            ScopeCoverage = "Inspected Program.cs and Service.cs.",
            CrossScopeAnalysis = "No further dependency tracing was required.",
            WhyNoIssueWasFound = "No issue matching the rule was found.",
        };

    private static RuleReviewWorkflowResult CreateReviewResult(
        StoredProjectPlanTaskItem taskItem,
        string _,
        IReadOnlyList<StoredRuleReviewIssue> issues,
        NoIssueConclusion? noIssueConclusion,
        bool reviewVerifierApproved,
        bool shouldEnterReportAggregation,
        bool continuedAfterVerifierRejectionLimit = false,
        bool stoppedAfterMissingSubmissionLimit = false) =>
        new()
        {
            TaskItem = taskItem,
            RuleKey = RuleFileName,
            Issues = issues,
            NoIssueConclusion = noIssueConclusion,
            Verdict = new ReviewVerdict
            {
                Approved = reviewVerifierApproved,
                Message = reviewVerifierApproved ? "Review accepted." : "Review degraded.",
            },
            ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
            StoppedAfterMissingSubmissionLimit = stoppedAfterMissingSubmissionLimit,
            ReviewAttempts = 1,
            VerifierAttempts = 1,
            RuleReviewAgentResetCount = 0,
        };

    private static RuleReportWorkflowResult CreateReportResult(
        StoredProjectPlanTaskItem taskItem,
        string ruleFileName,
        string _,
        IReadOnlyList<StoredRuleReviewIssue> issues,
        bool reportVerifierApproved) =>
        new()
        {
            RuleKey = ruleFileName,
            TaskItem = taskItem,
            Diff = new RuleReportDiff
            {
                CreatedIssues = [CreateReportIssue()],
                UpdatedIssues = [],
                DeletedIssues = [],
            },
            RepositoryIssues = [CreateReportIssue()],
            Verdict = new ReviewVerdict
            {
                Approved = reportVerifierApproved,
                Message = reportVerifierApproved ? "Report accepted." : "Report degraded.",
            },
            ContinuedAfterVerifierRejectionLimit = !reportVerifierApproved,
            AggregatorAttempts = 1,
            VerifierAttempts = 1,
        };

    private static StoredRuleReportIssue CreateReportIssue() =>
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
}
