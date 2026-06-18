using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Workflows.Report;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class RuleReportRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRuleReportRunnerFactory
{
    internal static string SummaryPromptAssetPath => ReportPromptAssetPaths.ReportSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Task<Result<RuleReportWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        OperationalContextCompactionOptions compactionOptions,
        IRuleReportIssueStore reportIssueStore,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportWorkflow workflow = new(
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, eventScope) => new ReportAggregatorAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, reportIssueStore, verdictBuffer, eventScope),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, eventScope) => new ReportVerifierAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, reportIssueStore, verdictBuffer, eventScope),
            reportIssueStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken);
    }

    internal static RuleReportWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
