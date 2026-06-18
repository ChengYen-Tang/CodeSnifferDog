using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Workflows.RuleFlow;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class RuleFlowRunnerFactory(
    RuleReviewRunnerFactory ruleReviewRunnerFactory,
    RuleReportRunnerFactory ruleReportRunnerFactory)
{
    private readonly RuleReviewRunnerFactory _ruleReviewRunnerFactory = ruleReviewRunnerFactory;
    private readonly RuleReportRunnerFactory _ruleReportRunnerFactory = ruleReportRunnerFactory;

    public Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
        RunnerFactoryContext context,
        OperationalContextCompactionOptions ruleReviewCompactionOptions,
        OperationalContextCompactionOptions reportCompactionOptions,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore) =>
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
        RunnerFactoryContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextCompactionOptions ruleReviewCompactionOptions,
        OperationalContextCompactionOptions reportCompactionOptions,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        CancellationToken cancellationToken)
    {
        RuleFlowWorkflow workflow = new(
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
