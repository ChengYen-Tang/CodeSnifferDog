using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;

using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Workflows.RuleReview;
using FluentResults;
using System.Diagnostics;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using RuleReviewWorkflowOptions = CodeSnifferDog.Models.RuleReview.WorkflowOptions;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview;

internal sealed class RunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRunnerFactory
{
    internal static string SummaryPromptAssetPath => RuleReviewAgentPromptAssets.RuleReviewSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<RunnerFactory> _logger = loggerFactory.CreateLogger<RunnerFactory>();

    public async Task<Result<RuleReviewWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        CompactionOptions compactionOptions,
        IIssueStore issueStore,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Rule review workflow started for rule {RuleKey}, task item {ProjectPlanTaskItemId}.",
            ruleKey,
            taskItem.ProjectPlanTaskItemId);

        ReviewVerdictBuffer verdictBuffer = new();
        Workflow workflow = new(
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new AgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer, eventScope),
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new VerifierFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer, eventScope),
            issueStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        Result<RuleReviewWorkflowResult> result =
            await workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Rule review workflow completed in {DurationMs} ms for rule {RuleKey}, task item {ProjectPlanTaskItemId}. Success: {Succeeded}; issue count: {IssueCount}.",
            stopwatch.ElapsedMilliseconds,
            ruleKey,
            taskItem.ProjectPlanTaskItemId,
            result.IsSuccess,
            result.IsSuccess ? result.Value.Issues.Count : 0);
        return result;
    }

    internal static RuleReviewWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
