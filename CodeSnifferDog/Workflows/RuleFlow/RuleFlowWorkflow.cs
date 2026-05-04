using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using FluentResults;

namespace CodeSnifferDog.Workflows.RuleFlow;

public sealed class RuleFlowWorkflow(
    Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleReviewWorkflowResult>>> ruleReviewWorkflowRunner,
    Func<string, string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, CancellationToken, Task<Result<RuleReportWorkflowResult>>> ruleReportWorkflowRunner)
{
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleReviewWorkflowResult>>> _ruleReviewWorkflowRunner = ruleReviewWorkflowRunner;
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, CancellationToken, Task<Result<RuleReportWorkflowResult>>> _ruleReportWorkflowRunner = ruleReportWorkflowRunner;

    public async Task<Result<RuleFlowWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
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
                RuleFlowCompletionState.DegradedMissingSubmission));
        }

        if (reviewResult.Value.Issues.Count == 0)
        {
            RuleFlowCompletionState completionState = reviewResult.Value.Verdict.Approved
                ? RuleFlowCompletionState.ApprovedNoIssue
                : RuleFlowCompletionState.DegradedNoIssue;

            return Result.Ok(CreateResult(
                reviewResult.Value,
                reportResult: null,
                completionState));
        }

        Result<RuleReportWorkflowResult> reportResult = await _ruleReportWorkflowRunner(
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            reviewResult.Value.Issues,
            cancellationToken).ConfigureAwait(false);

        if (reportResult.IsFailed)
            return reportResult.ToResult<RuleFlowWorkflowResult>();

        RuleFlowCompletionState reportCompletionState =
            reviewResult.Value.Verdict.Approved && reportResult.Value.Verdict.Approved
                ? RuleFlowCompletionState.ApprovedWithReport
                : RuleFlowCompletionState.DegradedWithReport;

        return Result.Ok(CreateResult(
            reviewResult.Value,
            reportResult.Value,
            reportCompletionState));
    }

    private static RuleFlowWorkflowResult CreateResult(
        RuleReviewWorkflowResult reviewResult,
        RuleReportWorkflowResult? reportResult,
        RuleFlowCompletionState completionState) =>
        new()
        {
            ReviewResult = reviewResult,
            ReportResult = reportResult,
            CompletionState = completionState,
        };
}
