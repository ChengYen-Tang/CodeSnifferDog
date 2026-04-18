using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using FluentResults;

namespace CodeSnifferDog.Workflows.RuleFlow;

public sealed class RuleFlowWorkflow(
    Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleReviewWorkflowResult>>> ruleReviewWorkflowRunner,
    Func<string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, CancellationToken, Task<Result<RuleReportWorkflowResult>>> ruleReportWorkflowRunner)
{
    private readonly Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleReviewWorkflowResult>>> _ruleReviewWorkflowRunner = ruleReviewWorkflowRunner;
    private readonly Func<string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, CancellationToken, Task<Result<RuleReportWorkflowResult>>> _ruleReportWorkflowRunner = ruleReportWorkflowRunner;

    public async Task<Result<RuleFlowWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RuleFlowWorkflowResult>("Repository root path is required.");

        if (string.IsNullOrWhiteSpace(ruleMarkdown))
            return Result.Fail<RuleFlowWorkflowResult>("Rule markdown is required.");

        ArgumentNullException.ThrowIfNull(taskItem);

        repositoryRootPath = repositoryRootPath.Trim();
        ruleMarkdown = ruleMarkdown.Trim();

        Result<RuleReviewWorkflowResult> reviewResult =
            await _ruleReviewWorkflowRunner(repositoryRootPath, ruleMarkdown, taskItem, cancellationToken).ConfigureAwait(false);

        if (reviewResult.IsFailed)
            return reviewResult.ToResult<RuleFlowWorkflowResult>();

        if (reviewResult.Value.StoppedAfterMissingSubmissionLimit)
            return Result.Ok(CreateResult(taskItem, ruleMarkdown, reviewResult.Value, reportResult: null, enteredReportAggregation: false, RuleFlowCompletionState.DegradedMissingSubmission));

        if (!reviewResult.Value.ShouldEnterReportAggregation)
        {
            RuleFlowCompletionState completionState = reviewResult.Value.ReviewVerifierApproved
                ? RuleFlowCompletionState.ApprovedNoIssue
                : RuleFlowCompletionState.DegradedNoIssue;

            return Result.Ok(CreateResult(taskItem, ruleMarkdown, reviewResult.Value, reportResult: null, enteredReportAggregation: false, completionState));
        }

        Result<RuleReportWorkflowResult> reportResult = await _ruleReportWorkflowRunner(
            repositoryRootPath,
            ruleMarkdown,
            taskItem,
            reviewResult.Value.Issues,
            cancellationToken).ConfigureAwait(false);

        if (reportResult.IsFailed)
            return reportResult.ToResult<RuleFlowWorkflowResult>();

        RuleFlowCompletionState reportCompletionState =
            reviewResult.Value.ReviewVerifierApproved && reportResult.Value.ReportVerifierApproved
                ? RuleFlowCompletionState.ApprovedWithReport
                : RuleFlowCompletionState.DegradedWithReport;

        return Result.Ok(CreateResult(taskItem, ruleMarkdown, reviewResult.Value, reportResult.Value, enteredReportAggregation: true, reportCompletionState));
    }

    private static RuleFlowWorkflowResult CreateResult(
        StoredProjectPlanTaskItem taskItem,
        string ruleMarkdown,
        RuleReviewWorkflowResult reviewResult,
        RuleReportWorkflowResult? reportResult,
        bool enteredReportAggregation,
        RuleFlowCompletionState completionState) =>
        new()
        {
            TaskItem = taskItem,
            RuleMarkdown = ruleMarkdown,
            ReviewResult = reviewResult,
            ReportResult = reportResult,
            EnteredReportAggregation = enteredReportAggregation,
            CompletionState = completionState,
        };
}
