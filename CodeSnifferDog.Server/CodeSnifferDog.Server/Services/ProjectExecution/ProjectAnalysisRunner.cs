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
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectAnalysisRunner(
    IProjectChatClientProvider chatClientProvider,
    IReviewRuleMarkdownProvider ruleMarkdownProvider,
    IOptions<ProjectExecutionOptions> options,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    ILogger<ProjectAnalysisRunner> logger) : IProjectAnalysisRunner
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IReviewRuleMarkdownProvider _ruleMarkdownProvider = ruleMarkdownProvider;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ProjectAnalysisRunner> _logger = logger;

    public bool IsReady => _chatClientProvider.IsReady && _ruleMarkdownProvider.HasRules;

    public async Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsReady)
            throw new InvalidOperationException("Project analysis runner is not ready.");

        if (_options.MaxParallelAgents <= 0)
            throw new InvalidOperationException("ExecutionOptions:MaxParallelAgents must be greater than zero.");

        if (_options.ModelContextWindowTokens <= 0)
            throw new InvalidOperationException("ExecutionOptions:ModelContextWindowTokens must be greater than zero.");

        IChatClient chatClient = _chatClientProvider.CreateChatClient();
        IReadOnlyList<string> ruleMarkdowns = await _ruleMarkdownProvider.LoadRuleMarkdownsAsync(cancellationToken);

        if (ruleMarkdowns.Count == 0)
            throw new InvalidOperationException("No review rule markdown files were found.");

        ReviewAgentTeamFactory teamFactory = CreateTeamFactory(chatClient);
        await using ReviewAgentTeamWorker worker = teamFactory.CreateWorker(
            context.RepositoryRootPath,
            ruleMarkdowns,
            new ReviewAgentTeamExecutionOptions
            {
                MaxParallelAgents = _options.MaxParallelAgents,
                ModelContextWindowTokens = _options.ModelContextWindowTokens,
                ContextCompactionMode = _options.ContextCompactionMode,
            });

        Result<ReviewAgentTeamRunResult> result = await worker.AnalyzeAsync(cancellationToken);
        if (result.IsFailed)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        _logger.LogInformation("Project {ProjectId} analysis completed.", context.ProjectId);
    }

    private ReviewAgentTeamFactory CreateTeamFactory(IChatClient chatClient)
    {
        PromptAssetReader promptAssetReader = new();
        ChatClientOperationalContextCompactionSummarizer summarizer = new(chatClient);
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory = new(promptAssetReader, summarizer);
        AgentCompactionOptions agentCompactionOptions = CreateAgentCompactionOptions(compactionOptionsFactory);

        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();
        InMemoryRuleReportIssueStore ruleReportIssueStore = new();

        return new ReviewAgentTeamFactory(new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                RunScanWorkflowAsync(chatClient, repositoryRootPath, agentCompactionOptions.Scan, promptAssetReader, cancellationToken),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                RunProjectPlanWorkflowAsync(chatClient, repositoryRootPath, scanProject, agentCompactionOptions.ProjectPlan, promptAssetReader, cancellationToken),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                RunRuleFlowWorkflowAsync(
                    chatClient,
                    repositoryRootPath,
                    ruleMarkdown,
                    taskItem,
                    agentCompactionOptions.RuleReview,
                    agentCompactionOptions.Report,
                    ruleReviewIssueStore,
                    ruleReportIssueStore,
                    promptAssetReader,
                    cancellationToken),
        });
    }

    private AgentCompactionOptions CreateAgentCompactionOptions(OperationalContextAgentCompactionOptionsFactory factory)
    {
        OperationalContextCompactionOptions options = new()
        {
            ModelContextWindowTokens = _options.ModelContextWindowTokens,
            Mode = _options.ContextCompactionMode,
        };

        return new AgentCompactionOptions
        {
            Scan = factory.CreateFromPromptAsset(ScanPromptAssetPaths.ScanSummaryPrompt, options),
            ProjectPlan = factory.CreateFromPromptAsset(ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt, options),
            RuleReview = factory.CreateFromPromptAsset(RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt, options),
            Report = factory.CreateFromPromptAsset(ReportPromptAssetPaths.ReportSummaryPrompt, options),
        };
    }

    private Task<Result<ScanWorkflowResult>> RunScanWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        OperationalContextAgentCompactionOptions compactionOptions,
        PromptAssetReader promptAssetReader,
        CancellationToken cancellationToken)
    {
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ScanAgentFactory scanAgentFactory = new(compactionOptions, promptAssetReader, _loggerFactory, _serviceProvider);
        ScanVerifierAgentFactory scanVerifierAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ScanWorkflow workflow = new(
            scanRepositoryRootPath => scanAgentFactory.Create(chatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer),
            scanRepositoryRootPath => scanVerifierAgentFactory.Create(chatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader);

        return workflow.RunAsync(repositoryRootPath, cancellationToken);
    }

    private Task<Result<ProjectPlanWorkflowResult>> RunProjectPlanWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        StoredScanProject scanProject,
        OperationalContextAgentCompactionOptions compactionOptions,
        PromptAssetReader promptAssetReader,
        CancellationToken cancellationToken)
    {
        InMemoryProjectPlanTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ProjectPlanAgentFactory projectPlanAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ProjectVerifierAgentFactory projectVerifierAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ProjectPlanWorkflow workflow = new(
            planRepositoryRootPath => projectPlanAgentFactory.Create(chatClient, planRepositoryRootPath, taskItemStore, verdictBuffer),
            (planRepositoryRootPath, verifierScanProject) => projectVerifierAgentFactory.Create(chatClient, planRepositoryRootPath, verifierScanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader);

        return workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken);
    }

    private Task<Result<RuleFlowWorkflowResult>> RunRuleFlowWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextAgentCompactionOptions ruleReviewCompactionOptions,
        OperationalContextAgentCompactionOptions reportCompactionOptions,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        PromptAssetReader promptAssetReader,
        CancellationToken cancellationToken)
    {
        RuleFlowWorkflow workflow = new(
            (reviewRepositoryRootPath, reviewRuleMarkdown, reviewTaskItem, reviewCancellationToken) =>
                RunRuleReviewWorkflowAsync(
                    chatClient,
                    reviewRepositoryRootPath,
                    reviewRuleMarkdown,
                    reviewTaskItem,
                    ruleReviewCompactionOptions,
                    ruleReviewIssueStore,
                    promptAssetReader,
                    reviewCancellationToken),
            (reportRepositoryRootPath, reportRuleMarkdown, reportTaskItem, currentFlowIssues, reportCancellationToken) =>
                RunRuleReportWorkflowAsync(
                    chatClient,
                    reportRepositoryRootPath,
                    reportRuleMarkdown,
                    reportTaskItem,
                    currentFlowIssues,
                    reportCompactionOptions,
                    ruleReportIssueStore,
                    promptAssetReader,
                    reportCancellationToken));

        return workflow.RunAsync(repositoryRootPath, ruleMarkdown, taskItem, cancellationToken);
    }

    private Task<Result<RuleReviewWorkflowResult>> RunRuleReviewWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextAgentCompactionOptions compactionOptions,
        IRuleReviewIssueStore issueStore,
        PromptAssetReader promptAssetReader,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReviewAgentFactory ruleReviewAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ReviewVerifierAgentFactory reviewVerifierAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        RuleReviewWorkflow workflow = new(
            (reviewRepositoryRootPath, reviewRuleMarkdown, reviewTaskItem) => ruleReviewAgentFactory.Create(chatClient, reviewRepositoryRootPath, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer),
            (reviewRepositoryRootPath, reviewRuleMarkdown, reviewTaskItem) => reviewVerifierAgentFactory.Create(chatClient, reviewRepositoryRootPath, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            promptAssetReader);

        return workflow.RunAsync(repositoryRootPath, ruleMarkdown, taskItem, cancellationToken);
    }

    private Task<Result<RuleReportWorkflowResult>> RunRuleReportWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        OperationalContextAgentCompactionOptions compactionOptions,
        IRuleReportIssueStore reportIssueStore,
        PromptAssetReader promptAssetReader,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        ReportAggregatorAgentFactory reportAggregatorAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ReportVerifierAgentFactory reportVerifierAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        RuleReportWorkflow workflow = new(
            (reportRepositoryRootPath, reportRuleMarkdown, reportTaskItem) => reportAggregatorAgentFactory.Create(chatClient, reportRepositoryRootPath, reportRuleMarkdown, reportTaskItem, reportIssueStore, verdictBuffer),
            (reportRepositoryRootPath, reportRuleMarkdown, reportTaskItem, issues) => reportVerifierAgentFactory.Create(chatClient, reportRepositoryRootPath, reportRuleMarkdown, reportTaskItem, issues, reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            promptAssetReader);

        return workflow.RunAsync(repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken);
    }

    private sealed class AgentCompactionOptions
    {
        public required OperationalContextAgentCompactionOptions Scan { get; init; }

        public required OperationalContextAgentCompactionOptions ProjectPlan { get; init; }

        public required OperationalContextAgentCompactionOptions RuleReview { get; init; }

        public required OperationalContextAgentCompactionOptions Report { get; init; }
    }
}
