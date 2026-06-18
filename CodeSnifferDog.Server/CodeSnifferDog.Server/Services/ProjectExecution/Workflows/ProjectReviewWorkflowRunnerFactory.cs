using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using FluentResults;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class ProjectReviewWorkflowRunnerFactory : IProjectReviewWorkflowRunnerFactory
{
    private readonly ScanRunnerBuilder _scanRunnerBuilder;
    private readonly ProjectPlanRunnerBuilder _projectPlanRunnerBuilder;
    private readonly RuleFlowRunnerBuilder _ruleFlowRunnerBuilder;

    public ProjectReviewWorkflowRunnerFactory(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
        : this(
            new ScanRunnerFactory(loggerFactory, serviceProvider).CreateRunner,
            new ProjectPlanRunnerFactory(loggerFactory, serviceProvider).CreateRunner,
            new RuleFlowRunnerFactory(
                new RuleReviewRunnerFactory(loggerFactory, serviceProvider),
                new RuleReportRunnerFactory(loggerFactory, serviceProvider)).CreateRunner)
    {
    }

    internal ProjectReviewWorkflowRunnerFactory(
        ScanRunnerBuilder scanRunnerBuilder,
        ProjectPlanRunnerBuilder projectPlanRunnerBuilder,
        RuleFlowRunnerBuilder ruleFlowRunnerBuilder)
    {
        _scanRunnerBuilder = scanRunnerBuilder;
        _projectPlanRunnerBuilder = projectPlanRunnerBuilder;
        _ruleFlowRunnerBuilder = ruleFlowRunnerBuilder;
    }

    public ProjectReviewWorkflowRunners CreateRunners(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        ProjectReviewAgentCompactionSettings compactionSettings,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus)
    {
        PromptAssetReader promptAssetReader = new();
        RunnerFactoryContext context = new(
            chatClient,
            executionOptions,
            new OperationalContextAgentCompactionOptionsFactory(
                promptAssetReader,
                new ChatClientOperationalContextCompactionSummarizer(chatClient)),
            promptAssetReader,
            agentEventBus);

        return new ProjectReviewWorkflowRunners
        {
            ScanWorkflowRunner = _scanRunnerBuilder(context, compactionSettings.Scan),
            ProjectPlanWorkflowRunner = _projectPlanRunnerBuilder(context, compactionSettings.ProjectPlan),
            RuleFlowWorkflowRunner = _ruleFlowRunnerBuilder(
                context,
                compactionSettings.RuleReview,
                compactionSettings.Report,
                ruleReviewIssueStore,
            ruleReportIssueStore),
        };
    }

    internal delegate Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> ScanRunnerBuilder(
        RunnerFactoryContext context,
        OperationalContextCompactionOptions compactionOptions);

    internal delegate Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> ProjectPlanRunnerBuilder(
        RunnerFactoryContext context,
        OperationalContextCompactionOptions compactionOptions);

    internal delegate Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> RuleFlowRunnerBuilder(
        RunnerFactoryContext context,
        OperationalContextCompactionOptions ruleReviewCompactionOptions,
        OperationalContextCompactionOptions reportCompactionOptions,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore);
}
