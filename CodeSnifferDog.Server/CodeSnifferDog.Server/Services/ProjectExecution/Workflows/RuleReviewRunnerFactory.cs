using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Workflows.RuleReview;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class RuleReviewRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRuleReviewRunnerFactory
{
    internal static string SummaryPromptAssetPath => RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Task<Result<RuleReviewWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextCompactionOptions compactionOptions,
        IRuleReviewIssueStore issueStore,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReviewWorkflow workflow = new(
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new RuleReviewAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer, eventScope),
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new ReviewVerifierAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer, eventScope),
            issueStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken);
    }

    internal static RuleReviewWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
