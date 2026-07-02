using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Workflows.Report;
using FluentResults;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class RuleReportRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRuleReportRunnerFactory
{
    internal static string SummaryPromptAssetPath => ReportPromptAssetPaths.ReportSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<RuleReportRunnerFactory> _logger = loggerFactory.CreateLogger<RuleReportRunnerFactory>();

    public async Task<Result<RuleReportWorkflowResult>> RunAsync(
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
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Rule report workflow started for rule {RuleKey}, task item {ProjectPlanTaskItemId}. Current flow issue count: {IssueCount}.",
            ruleKey,
            taskItem.ProjectPlanTaskItemId,
            currentFlowIssues.Count);

        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportWorkflow workflow = new(
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, eventScope) => new ReportAggregatorAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, reportIssueStore, verdictBuffer, eventScope),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, eventScope) => new ReportVerifierAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, reportIssueStore, verdictBuffer, eventScope),
            reportIssueStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        Result<RuleReportWorkflowResult> result =
            await workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Rule report workflow completed in {DurationMs} ms for rule {RuleKey}, task item {ProjectPlanTaskItemId}. Success: {Succeeded}; repository issue count: {IssueCount}.",
            stopwatch.ElapsedMilliseconds,
            ruleKey,
            taskItem.ProjectPlanTaskItemId,
            result.IsSuccess,
            result.IsSuccess ? result.Value.RepositoryIssues.Count : 0);
        return result;
    }

    internal static RuleReportWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
