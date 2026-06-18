using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Workflows.ProjectPlan;
using CodeSnifferDog.Workflows.Report;
using CodeSnifferDog.Workflows.RuleFlow;
using CodeSnifferDog.Workflows.RuleReview;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectReviewAgentTeamWorkerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IProjectReviewAgentTeamWorkerFactory
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly WorkerFactory _workerFactory = DefaultWorkerFactory;

    internal ProjectReviewAgentTeamWorkerFactory(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        WorkerFactory workerFactory)
        : this(loggerFactory, serviceProvider)
    {
        _workerFactory = workerFactory;
    }

    public IProjectReviewAgentTeamWorker CreateWorker(
        IChatClient chatClient,
        string repositoryRootPath,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        InMemoryRuleReportIssueStore ruleReportIssueStore = new();
        ReviewAgentTeamDependencies dependencies = CreateTeamDependencies(
            chatClient,
            ruleReportIssueStore,
            executionOptions,
            agentEventBus);

        ReviewAgentTeamWorker worker = _workerFactory(
            dependencies,
            repositoryRootPath,
            rules.Select(rule => new ReviewAgentRuleDefinition
            {
                RuleKey = rule.RuleKey,
                RuleMarkdown = rule.RuleMarkdown,
            }).ToArray(),
            new ReviewAgentTeamExecutionOptions
            {
                MaxParallelAgents = executionOptions.MaxParallelAgents,
                ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
                ContextCompactionMode = executionOptions.ContextCompactionMode,
            });

        return new ProjectReviewAgentTeamWorker(worker);
    }

    private ReviewAgentTeamDependencies CreateTeamDependencies(
        IChatClient chatClient,
        InMemoryRuleReportIssueStore ruleReportIssueStore,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        PromptAssetReader promptAssetReader = new();
        ChatClientOperationalContextCompactionSummarizer summarizer = new(chatClient);
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory = new(promptAssetReader, summarizer);
        AgentCompactionSettings agentCompactionSettings = CreateAgentCompactionSettings(executionOptions);

        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();

        return new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                RunScanWorkflowAsync(chatClient, repositoryRootPath, agentCompactionSettings.Scan, compactionOptionsFactory, promptAssetReader, agentEventBus, executionOptions, cancellationToken),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                RunProjectPlanWorkflowAsync(chatClient, repositoryRootPath, scanProject, agentCompactionSettings.ProjectPlan, compactionOptionsFactory, promptAssetReader, agentEventBus, executionOptions, cancellationToken),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                RunRuleFlowWorkflowAsync(
                    chatClient,
                    repositoryRootPath,
                    ruleKey,
                    ruleMarkdown,
                    taskItem,
                    agentCompactionSettings.RuleReview,
                    agentCompactionSettings.Report,
                    compactionOptionsFactory,
                    ruleReviewIssueStore,
                    ruleReportIssueStore,
                    promptAssetReader,
                    agentEventBus,
                    executionOptions,
                    cancellationToken),
            RuleReportIssueStore = ruleReportIssueStore,
            AgentEventBus = agentEventBus,
        };
    }

    internal delegate ReviewAgentTeamWorker WorkerFactory(
        ReviewAgentTeamDependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        ReviewAgentTeamExecutionOptions executionOptions);

    private static ReviewAgentTeamWorker DefaultWorkerFactory(
        ReviewAgentTeamDependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        ReviewAgentTeamExecutionOptions executionOptions) =>
        new ReviewAgentTeamFactory(dependencies).CreateWorker(repositoryRootPath, ruleDefinitions, executionOptions);

    private static AgentCompactionSettings CreateAgentCompactionSettings(ExecutionOptions executionOptions)
    {
        OperationalContextCompactionOptions options = new()
        {
            ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
            Mode = executionOptions.ContextCompactionMode,
        };

        return new AgentCompactionSettings
        {
            Scan = options,
            ProjectPlan = options,
            RuleReview = options,
            Report = options,
        };
    }

    private Task<Result<ScanWorkflowResult>> RunScanWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        OperationalContextCompactionOptions compactionOptions,
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory,
        PromptAssetReader promptAssetReader,
        IAgentEventBus agentEventBus,
        ExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ScanWorkflow workflow = new(
            (scanRepositoryRootPath, eventScope) => new ScanAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ScanPromptAssetPaths.ScanSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                _loggerFactory,
                _serviceProvider).Create(chatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer, eventScope),
            (scanRepositoryRootPath, eventScope) => new ScanVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ScanPromptAssetPaths.ScanSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer, eventScope),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                AgentRunTimeout = executionOptions.AgentRunTimeout,
                MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            },
            agentEventBus: agentEventBus);

        return workflow.RunAsync(repositoryRootPath, cancellationToken);
    }

    private Task<Result<ProjectPlanWorkflowResult>> RunProjectPlanWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        StoredScanProject scanProject,
        OperationalContextCompactionOptions compactionOptions,
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory,
        PromptAssetReader promptAssetReader,
        IAgentEventBus agentEventBus,
        ExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        InMemoryProjectPlanTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ProjectPlanWorkflow workflow = new(
            (planRepositoryRootPath, eventScope) => new ProjectPlanAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, planRepositoryRootPath, taskItemStore, verdictBuffer, eventScope),
            (planRepositoryRootPath, verifierScanProject, eventScope) => new ProjectVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, planRepositoryRootPath, verifierScanProject, taskItemStore, verdictBuffer, eventScope),
            taskItemStore,
            verdictBuffer,
            promptAssetReader,
            new ProjectPlanWorkflowOptions
            {
                AgentRunTimeout = executionOptions.AgentRunTimeout,
                MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            },
            agentEventBus: agentEventBus);

        return workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken);
    }

    private Task<Result<RuleFlowWorkflowResult>> RunRuleFlowWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextCompactionOptions ruleReviewCompactionOptions,
        OperationalContextCompactionOptions reportCompactionOptions,
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        PromptAssetReader promptAssetReader,
        IAgentEventBus agentEventBus,
        ExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        RuleFlowWorkflow workflow = new(
            (reviewRepositoryRootPath, _, reviewRuleMarkdown, reviewTaskItem, reviewCancellationToken) =>
                RunRuleReviewWorkflowAsync(
                    chatClient,
                    reviewRepositoryRootPath,
                    ruleKey,
                    reviewRuleMarkdown,
                    reviewTaskItem,
                    ruleReviewCompactionOptions,
                    compactionOptionsFactory,
                    ruleReviewIssueStore,
                    promptAssetReader,
                    agentEventBus,
                    executionOptions,
                    reviewCancellationToken),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, currentFlowIssues, reportCancellationToken) =>
                RunRuleReportWorkflowAsync(
                    chatClient,
                    reportRepositoryRootPath,
                    reportRuleKey,
                    reportRuleMarkdown,
                    reportTaskItem,
                    currentFlowIssues,
                    reportCompactionOptions,
                    compactionOptionsFactory,
                    ruleReportIssueStore,
                    promptAssetReader,
                    agentEventBus,
                    executionOptions,
                    reportCancellationToken));

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken);
    }

    private Task<Result<RuleReviewWorkflowResult>> RunRuleReviewWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextCompactionOptions compactionOptions,
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory,
        IRuleReviewIssueStore issueStore,
        PromptAssetReader promptAssetReader,
        IAgentEventBus agentEventBus,
        ExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReviewWorkflow workflow = new(
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new RuleReviewAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer, eventScope),
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new ReviewVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer, eventScope),
            issueStore,
            verdictBuffer,
            promptAssetReader,
            new RuleReviewWorkflowOptions
            {
                AgentRunTimeout = executionOptions.AgentRunTimeout,
                MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            },
            agentEventBus: agentEventBus);

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken);
    }

    private Task<Result<RuleReportWorkflowResult>> RunRuleReportWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        OperationalContextCompactionOptions compactionOptions,
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory,
        IRuleReportIssueStore reportIssueStore,
        PromptAssetReader promptAssetReader,
        IAgentEventBus agentEventBus,
        ExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportWorkflow workflow = new(
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, eventScope) => new ReportAggregatorAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ReportPromptAssetPaths.ReportSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, reportIssueStore, verdictBuffer, eventScope),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, eventScope) => new ReportVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ReportPromptAssetPaths.ReportSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, reportIssueStore, verdictBuffer, eventScope),
            reportIssueStore,
            verdictBuffer,
            promptAssetReader,
            new RuleReportWorkflowOptions
            {
                AgentRunTimeout = executionOptions.AgentRunTimeout,
                MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            },
            agentEventBus: agentEventBus);

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken);
    }

    private static OperationalContextAgentCompactionOptions CreateCompactionOptions(
        OperationalContextAgentCompactionOptionsFactory factory,
        string summaryPromptAssetPath,
        OperationalContextCompactionOptions options,
        IAgentEventScope eventScope) =>
        factory.CreateFromPromptAsset(
            summaryPromptAssetPath,
            options,
            hooks:
            [
                new AgentCompactionEventHook(eventScope),
            ]);

    private sealed class AgentCompactionSettings
    {
        public required OperationalContextCompactionOptions Scan { get; init; }

        public required OperationalContextCompactionOptions ProjectPlan { get; init; }

        public required OperationalContextCompactionOptions RuleReview { get; init; }

        public required OperationalContextCompactionOptions Report { get; init; }
    }
}
