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
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Workflows.ProjectPlan;
using CodeSnifferDog.Workflows.Report;
using CodeSnifferDog.Workflows.RuleFlow;
using CodeSnifferDog.Workflows.RuleReview;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectAnalysisRunner(
    IProjectChatClientProvider chatClientProvider,
    IReviewRuleMarkdownProvider ruleMarkdownProvider,
    IProjectReportService projectReportService,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectAgentStatusLiveUpdateNotifier projectAgentStatusLiveUpdateNotifier,
    IOptions<ProjectExecutionOptions> options,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    ILogger<ProjectAnalysisRunner> logger,
    Func<ProjectAnalysisContext, IReadOnlyList<ProjectExecutionRuleDefinition>, CancellationToken, Task<ReviewAgentTeamAnalysisResult>>? analysisOverride = null) : IProjectAnalysisRunner
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IReviewRuleMarkdownProvider _ruleMarkdownProvider = ruleMarkdownProvider;
    private readonly IProjectReportService _projectReportService = projectReportService;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _projectAgentStatusLiveUpdateNotifier = projectAgentStatusLiveUpdateNotifier;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ProjectAnalysisRunner> _logger = logger;
    private readonly Func<ProjectAnalysisContext, IReadOnlyList<ProjectExecutionRuleDefinition>, CancellationToken, Task<ReviewAgentTeamAnalysisResult>>? _analysisOverride = analysisOverride;

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
        if (_options.AgentRunTimeoutSeconds <= 0)
            throw new InvalidOperationException("ExecutionOptions:AgentRunTimeoutSeconds must be greater than zero.");
        if (_options.MaxConsecutiveAgentRunFailures <= 0)
            throw new InvalidOperationException("ExecutionOptions:MaxConsecutiveAgentRunFailures must be greater than zero.");

        await ClearAgentStatusDataAsync(context.ProjectId, cancellationToken);

        IReadOnlyList<ProjectExecutionRuleDefinition> rules = await _ruleMarkdownProvider.LoadRulesAsync(cancellationToken);

        if (rules.Count == 0)
            throw new InvalidOperationException("No review rule markdown files were found.");

        if (_analysisOverride is not null)
        {
            ReviewAgentTeamAnalysisResult overrideResult =
                await _analysisOverride(context, rules, cancellationToken).ConfigureAwait(false);
            await CompleteAnalysisAsync(context.ProjectId, rules, overrideResult, cancellationToken).ConfigureAwait(false);
            return;
        }

        IChatClient chatClient = _chatClientProvider.CreateChatClient();
        InMemoryRuleReportIssueStore ruleReportIssueStore = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber eventSubscriber =
            new(context.ProjectId, _dbContextFactory, _projectAgentStatusLiveUpdateNotifier, eventStream.Events);
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory(chatClient, ruleReportIssueStore, eventStream);
        try
        {
            await using ReviewAgentTeamWorker worker = teamFactory.CreateWorker(
                context.RepositoryRootPath,
                rules.Select(rule => new ReviewAgentRuleDefinition
                {
                    RuleKey = rule.RuleKey,
                    RuleMarkdown = rule.RuleMarkdown,
                }).ToArray(),
                new ReviewAgentTeamExecutionOptions
                {
                    MaxParallelAgents = _options.MaxParallelAgents,
                    ModelContextWindowTokens = _options.ModelContextWindowTokens,
                    ContextCompactionMode = _options.ContextCompactionMode,
                });

            ReviewAgentTeamAnalysisResult analysisResult = await worker.AnalyzeDetailedAsync(cancellationToken);
            await CompleteAnalysisAsync(context.ProjectId, rules, analysisResult, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            eventStream.Complete();
        }
        _logger.LogInformation("Project {ProjectId} analysis completed.", context.ProjectId);
    }

    private async Task CompleteAnalysisAsync(
        Guid projectId,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ReviewAgentTeamAnalysisResult analysisResult,
        CancellationToken cancellationToken)
    {
        ReviewAgentTeamAnalysisCompletionDecision completionDecision =
            ReviewAgentTeamAnalysisCompletionPolicy.Evaluate(analysisResult);

        if (completionDecision.ShouldPersistReports)
            await PersistReportsAsync(projectId, rules, analysisResult.RuleReports, cancellationToken).ConfigureAwait(false);
        else
            await _projectReportService.ReplaceProjectReportsAsync(projectId, [], cancellationToken).ConfigureAwait(false);

        if (!completionDecision.IsSuccess)
            throw new InvalidOperationException(completionDecision.FailureMessage);
    }

    private async Task ClearAgentStatusDataAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<Data.Entities.ProjectAgentGroupRecord> existingGroups = await dbContext.ProjectAgentGroups
            .Where(group => group.ProjectId == projectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingGroups.Count == 0)
            return;

        dbContext.ProjectAgentGroups.RemoveRange(existingGroups);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private ReviewAgentTeamFactory CreateTeamFactory(
        IChatClient chatClient,
        InMemoryRuleReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus)
    {
        PromptAssetReader promptAssetReader = new();
        ChatClientOperationalContextCompactionSummarizer summarizer = new(chatClient);
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory = new(promptAssetReader, summarizer);
        AgentCompactionSettings agentCompactionSettings = CreateAgentCompactionSettings(compactionOptionsFactory);

        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();

        return new ReviewAgentTeamFactory(new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                RunScanWorkflowAsync(chatClient, repositoryRootPath, agentCompactionSettings.Scan, compactionOptionsFactory, promptAssetReader, agentEventBus, cancellationToken),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                RunProjectPlanWorkflowAsync(chatClient, repositoryRootPath, scanProject, agentCompactionSettings.ProjectPlan, compactionOptionsFactory, promptAssetReader, agentEventBus, cancellationToken),
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
                    cancellationToken),
            RuleReportIssueStore = ruleReportIssueStore,
            AgentEventBus = agentEventBus,
        });
    }

    private AgentCompactionSettings CreateAgentCompactionSettings(OperationalContextAgentCompactionOptionsFactory factory)
    {
        OperationalContextCompactionOptions options = new()
        {
            ModelContextWindowTokens = _options.ModelContextWindowTokens,
            Mode = _options.ContextCompactionMode,
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
                _serviceProvider).Create(chatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer),
            (scanRepositoryRootPath, eventScope) => new ScanVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ScanPromptAssetPaths.ScanSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                AgentRunTimeout = _options.AgentRunTimeout,
                MaxConsecutiveRunFailures = _options.MaxConsecutiveAgentRunFailures,
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
                serviceProvider: _serviceProvider).Create(chatClient, planRepositoryRootPath, taskItemStore, verdictBuffer),
            (planRepositoryRootPath, verifierScanProject, eventScope) => new ProjectVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, planRepositoryRootPath, verifierScanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader,
            new ProjectPlanWorkflowOptions
            {
                AgentRunTimeout = _options.AgentRunTimeout,
                MaxConsecutiveRunFailures = _options.MaxConsecutiveAgentRunFailures,
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
                serviceProvider: _serviceProvider).Create(chatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer),
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, eventScope) => new ReviewVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            promptAssetReader,
            new RuleReviewWorkflowOptions
            {
                AgentRunTimeout = _options.AgentRunTimeout,
                MaxConsecutiveRunFailures = _options.MaxConsecutiveAgentRunFailures,
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
                serviceProvider: _serviceProvider).Create(chatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, reportIssueStore, verdictBuffer),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, eventScope) => new ReportVerifierAgentFactory(
                CreateCompactionOptions(
                    compactionOptionsFactory,
                    ReportPromptAssetPaths.ReportSummaryPrompt,
                    compactionOptions,
                    eventScope),
                promptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(chatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            promptAssetReader,
            new RuleReportWorkflowOptions
            {
                AgentRunTimeout = _options.AgentRunTimeout,
                MaxConsecutiveRunFailures = _options.MaxConsecutiveAgentRunFailures,
            },
            agentEventBus: agentEventBus);

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, cancellationToken);
    }

    private async Task PersistReportsAsync(
        Guid projectId,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        IReadOnlyList<ReviewAgentTeamRuleReport> ruleReports,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> ruleNamesByKey = rules.ToDictionary(rule => rule.RuleKey, rule => rule.RuleName, StringComparer.Ordinal);
        List<ProjectRuleReportDraft> drafts = [];
        foreach (ReviewAgentTeamRuleReport ruleReport in ruleReports)
        {
            if (!ruleNamesByKey.TryGetValue(ruleReport.RuleKey, out string? ruleName))
                throw new InvalidOperationException($"Rule name mapping was not found for rule key '{ruleReport.RuleKey}'.");

            drafts.Add(new ProjectRuleReportDraft
            {
                RuleKey = ruleReport.RuleKey,
                RuleName = ruleName,
                MarkdownContent = ruleReport.MarkdownContent,
            });
        }

        await _projectReportService.ReplaceProjectReportsAsync(projectId, drafts, cancellationToken);
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
