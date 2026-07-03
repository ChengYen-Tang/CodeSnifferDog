using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Workflows.RuleFlow;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ReviewRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.IRunnerFactory;
using ReportRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.IRunnerFactory;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow;

internal sealed class RunnerFactory(
    ReviewRunnerFactoryInterface ruleReviewRunnerFactory,
    ReportRunnerFactoryInterface ruleReportRunnerFactory) : IRunnerFactory
{
    private readonly ReviewRunnerFactoryInterface _ruleReviewRunnerFactory = ruleReviewRunnerFactory;
    private readonly ReportRunnerFactoryInterface _ruleReportRunnerFactory = ruleReportRunnerFactory;

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

    private Task<Result<RuleFlowWorkflowResult>> RunAsync(
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

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken);
    }
}
