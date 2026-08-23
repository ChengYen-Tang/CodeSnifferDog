using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;

using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Workflows.Report;
using CodeSnifferDog.Workflows.Adapters.AgentFramework.Contracts;
using FluentResults;
using System.Diagnostics;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ReportWorkflowOptions = CodeSnifferDog.Models.Report.WorkflowOptions;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport;

/// <summary>
/// Runs the workflow stage that turns reviewed issues into repository-level report issues.
/// </summary>
internal sealed class RunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRunnerFactory
{
    /// <summary>
    /// Gets the prompt asset used when report agents summarize compacted history.
    /// </summary>
    internal static string SummaryPromptAssetPath => ReportAgentPromptAssets.ReportSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<RunnerFactory> _logger = loggerFactory.CreateLogger<RunnerFactory>();

    /// <inheritdoc />
    public async Task<Result<ReportWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IReadOnlyList<RuleReviewStoredIssue> currentFlowIssues,
        CompactionOptions compactionOptions,
        IIssueStore reportIssueStore,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Rule report workflow started for rule {RuleKey}, task item {ProjectPlanTaskItemId}. Current flow issue count: {IssueCount}.",
            ruleKey,
            taskItem.ProjectPlanTaskItemId,
            currentFlowIssues.Count);

        ReviewVerdictBuffer verdictBuffer = new();
        Workflow workflow = new(
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
            agentEventBus: context.AgentEventBus,
            logger: _logger);

        Result<ReportWorkflowResult> result = await context.WorkflowRuntime.RunAsync(
            executorId: "report",
            input: new ReportRequest(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues),
            operation: (request, token) => workflow.RunAsync(
                request.RepositoryRootPath,
                request.RuleKey,
                request.RuleMarkdown,
                request.TaskItem,
                request.CurrentFlowIssues,
                token),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Rule report workflow completed in {DurationMs} ms for rule {RuleKey}, task item {ProjectPlanTaskItemId}. Success: {Succeeded}; repository issue count: {IssueCount}.",
            stopwatch.ElapsedMilliseconds,
            ruleKey,
            taskItem.ProjectPlanTaskItemId,
            result.IsSuccess,
            result.IsSuccess ? result.Value.RepositoryIssues.Count : 0);
        return result;
    }

    /// <summary>
    /// Creates workflow options for report runs from the configured execution limits.
    /// </summary>
    /// <param name="executionOptions">Execution limits shared across review workflows.</param>
    /// <returns>The workflow options passed to the report workflow.</returns>
    internal static ReportWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            MaxMissingSubmissionAttempts = executionOptions.MaxMissingSubmissionAttempts,
            MaxVerifierRejectionAttempts = executionOptions.MaxVerifierRejectionAttempts,
        };
}
