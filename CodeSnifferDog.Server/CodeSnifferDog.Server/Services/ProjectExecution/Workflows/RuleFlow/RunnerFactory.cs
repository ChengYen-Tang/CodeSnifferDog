using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Workflows.RuleFlow;
using CodeSnifferDog.Workflows.Adapters.AgentFramework.Contracts;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ReviewRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.IRunnerFactory;
using ReportRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.IRunnerFactory;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow;

/// <summary>
/// Creates the runner that chains rule review and rule report workflows into a single flow.
/// </summary>
internal sealed class RunnerFactory(
    ReviewRunnerFactoryInterface ruleReviewRunnerFactory,
    ReportRunnerFactoryInterface ruleReportRunnerFactory) : IRunnerFactory
{
    private readonly ReviewRunnerFactoryInterface _ruleReviewRunnerFactory = ruleReviewRunnerFactory;
    private readonly ReportRunnerFactoryInterface _ruleReportRunnerFactory = ruleReportRunnerFactory;

    /// <inheritdoc />
    public Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions ruleReviewCompactionOptions,
        CompactionOptions reportCompactionOptions,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore) =>
        (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            RunAsync(
                context,
                repositoryRootPath,
                ruleKey,
                ruleMarkdown,
                taskItem,
                ruleReviewCompactionOptions,
                reportCompactionOptions,
                ruleReviewIssueStore,
                ruleReportIssueStore,
                cancellationToken);

    private async Task<Result<RuleFlowWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        CompactionOptions ruleReviewCompactionOptions,
        CompactionOptions reportCompactionOptions,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore,
        CancellationToken cancellationToken)
    {
        Workflow workflow = new(
            (reviewRepositoryRootPath, _, reviewRuleMarkdown, reviewTaskItem, reviewCancellationToken) =>
                _ruleReviewRunnerFactory.RunAsync(
                    context,
                    reviewRepositoryRootPath,
                    ruleKey,
                    reviewRuleMarkdown,
                    reviewTaskItem,
                    ruleReviewCompactionOptions,
                    ruleReviewIssueStore,
                    reviewCancellationToken),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, currentFlowIssues, reportCancellationToken) =>
                _ruleReportRunnerFactory.RunAsync(
                    context,
                    reportRepositoryRootPath,
                    reportRuleKey,
                    reportRuleMarkdown,
                    reportTaskItem,
                    currentFlowIssues,
                    reportCompactionOptions,
                    ruleReportIssueStore,
                    reportCancellationToken));

        return await context.WorkflowRuntime.RunAsync(
            executorId: "rule-flow",
            input: new RuleFlowRequest(repositoryRootPath, ruleKey, ruleMarkdown, taskItem),
            operation: (request, token) => workflow.RunAsync(
                request.RepositoryRootPath,
                request.RuleKey,
                request.RuleMarkdown,
                request.TaskItem,
                token),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
