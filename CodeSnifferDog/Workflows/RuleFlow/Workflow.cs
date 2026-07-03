using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using FluentResults;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Workflows.RuleFlow;

public sealed class Workflow(
    Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleReviewWorkflowResult>>> ruleReviewWorkflowRunner,
    Func<string, string, string, StoredTaskItem, IReadOnlyList<RuleReviewStoredIssue>, CancellationToken, Task<Result<ReportWorkflowResult>>> ruleReportWorkflowRunner)
{
    private readonly Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleReviewWorkflowResult>>> _ruleReviewWorkflowRunner = ruleReviewWorkflowRunner;
    private readonly Func<string, string, string, StoredTaskItem, IReadOnlyList<RuleReviewStoredIssue>, CancellationToken, Task<Result<ReportWorkflowResult>>> _ruleReportWorkflowRunner = ruleReportWorkflowRunner;

    public async Task<Result<RuleFlowWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RuleFlowWorkflowResult>("Repository root path is required.");

        if (string.IsNullOrWhiteSpace(ruleMarkdown))
            return Result.Fail<RuleFlowWorkflowResult>("Rule markdown is required.");

        if (string.IsNullOrWhiteSpace(ruleKey))
            return Result.Fail<RuleFlowWorkflowResult>("Rule key is required.");

        ArgumentNullException.ThrowIfNull(taskItem);

        repositoryRootPath = repositoryRootPath.Trim();
        ruleKey = ruleKey.Trim();
        ruleMarkdown = ruleMarkdown.Trim();

        Result<RuleReviewWorkflowResult> reviewResult =
            await _ruleReviewWorkflowRunner(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken).ConfigureAwait(false);

        if (reviewResult.IsFailed)
            return reviewResult.ToResult<RuleFlowWorkflowResult>();

        if (reviewResult.Value.StoppedAfterMissingSubmissionLimit)
        {
            return Result.Ok(CreateResult(
                reviewResult.Value,
                reportResult: null,
                CompletionState.DegradedMissingSubmission));
        }

        if (reviewResult.Value.Issues.Count == 0)
        {
            CompletionState completionState = reviewResult.Value.Verdict.Approved
                ? CompletionState.ApprovedNoIssue
                : CompletionState.DegradedNoIssue;

            return Result.Ok(CreateResult(
                reviewResult.Value,
                reportResult: null,
                completionState));
        }

        Result<ReportWorkflowResult> reportResult = await _ruleReportWorkflowRunner(
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            reviewResult.Value.Issues,
            cancellationToken).ConfigureAwait(false);

        if (reportResult.IsFailed)
            return reportResult.ToResult<RuleFlowWorkflowResult>();

        CompletionState reportCompletionState =
            reviewResult.Value.Verdict.Approved && reportResult.Value.Verdict.Approved
                ? CompletionState.ApprovedWithReport
                : CompletionState.DegradedWithReport;

        return Result.Ok(CreateResult(
            reviewResult.Value,
            reportResult.Value,
            reportCompletionState));
    }

    private static RuleFlowWorkflowResult CreateResult(
        RuleReviewWorkflowResult reviewResult,
        ReportWorkflowResult? reportResult,
        CompletionState completionState) =>
        new()
        {
            ReviewResult = reviewResult,
            ReportResult = reportResult,
            CompletionState = completionState,
        };
}
