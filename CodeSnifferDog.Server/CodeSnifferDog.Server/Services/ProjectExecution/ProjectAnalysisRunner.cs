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
using CodeSnifferDog.Server.Data;
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
    IOptions<ProjectExecutionOptions> options,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    ILogger<ProjectAnalysisRunner> logger) : IProjectAnalysisRunner
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IReviewRuleMarkdownProvider _ruleMarkdownProvider = ruleMarkdownProvider;
    private readonly IProjectReportService _projectReportService = projectReportService;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
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

        await ClearAgentStatusDataAsync(context.ProjectId, cancellationToken);

        IChatClient chatClient = _chatClientProvider.CreateChatClient();
        IReadOnlyList<ProjectExecutionRuleDefinition> rules = await _ruleMarkdownProvider.LoadRulesAsync(cancellationToken);

        if (rules.Count == 0)
            throw new InvalidOperationException("No review rule markdown files were found.");

        InMemoryRuleReportIssueStore ruleReportIssueStore = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber eventSubscriber =
            new(context.ProjectId, _dbContextFactory, eventStream.Events);
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory(chatClient, ruleReportIssueStore, eventStream);
        IReadOnlyList<ReviewAgentTeamRuleReport> ruleReports;

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

            Result result = await worker.AnalyzeAsync(cancellationToken);
            if (result.IsFailed)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

            ruleReports = await worker.GetRuleReportsAsync(cancellationToken);
        }
        finally
        {
            eventStream.Complete();
        }

        await PersistReportsAsync(context.ProjectId, rules, ruleReports, cancellationToken);
        _logger.LogInformation("Project {ProjectId} analysis completed.", context.ProjectId);
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
        IAgentStatusEventPublisher agentStatusEventPublisher)
    {
        PromptAssetReader promptAssetReader = new();
        ChatClientOperationalContextCompactionSummarizer summarizer = new(chatClient);
        OperationalContextAgentCompactionOptionsFactory compactionOptionsFactory = new(promptAssetReader, summarizer);
        AgentCompactionOptions agentCompactionOptions = CreateAgentCompactionOptions(compactionOptionsFactory);

        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();

        return new ReviewAgentTeamFactory(new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                RunScanWorkflowAsync(chatClient, repositoryRootPath, agentCompactionOptions.Scan, promptAssetReader, agentStatusEventPublisher, cancellationToken),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                RunProjectPlanWorkflowAsync(chatClient, repositoryRootPath, scanProject, agentCompactionOptions.ProjectPlan, promptAssetReader, agentStatusEventPublisher, cancellationToken),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                RunRuleFlowWorkflowAsync(
                    chatClient,
                    repositoryRootPath,
                    ruleKey,
                    ruleMarkdown,
                    taskItem,
                    agentCompactionOptions.RuleReview,
                    agentCompactionOptions.Report,
                    ruleReviewIssueStore,
                    ruleReportIssueStore,
                    promptAssetReader,
                    agentStatusEventPublisher,
                    cancellationToken),
            RuleReportIssueStore = ruleReportIssueStore,
            AgentStatusEventPublisher = agentStatusEventPublisher,
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
        IAgentStatusEventPublisher agentStatusEventPublisher,
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
            promptAssetReader,
            agentStatusEventPublisher: agentStatusEventPublisher);

        return workflow.RunAsync(repositoryRootPath, cancellationToken);
    }

    private Task<Result<ProjectPlanWorkflowResult>> RunProjectPlanWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        StoredScanProject scanProject,
        OperationalContextAgentCompactionOptions compactionOptions,
        PromptAssetReader promptAssetReader,
        IAgentStatusEventPublisher agentStatusEventPublisher,
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
            promptAssetReader,
            agentStatusEventPublisher: agentStatusEventPublisher);

        return workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken);
    }

    private Task<Result<RuleFlowWorkflowResult>> RunRuleFlowWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextAgentCompactionOptions ruleReviewCompactionOptions,
        OperationalContextAgentCompactionOptions reportCompactionOptions,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        PromptAssetReader promptAssetReader,
        IAgentStatusEventPublisher agentStatusEventPublisher,
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
                    ruleReviewIssueStore,
                    promptAssetReader,
                    agentStatusEventPublisher,
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
                    ruleReportIssueStore,
                    promptAssetReader,
                    agentStatusEventPublisher,
                    reportCancellationToken));

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken);
    }

    private Task<Result<RuleReviewWorkflowResult>> RunRuleReviewWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextAgentCompactionOptions compactionOptions,
        IRuleReviewIssueStore issueStore,
        PromptAssetReader promptAssetReader,
        IAgentStatusEventPublisher agentStatusEventPublisher,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReviewAgentFactory ruleReviewAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ReviewVerifierAgentFactory reviewVerifierAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        RuleReviewWorkflow workflow = new(
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem) => ruleReviewAgentFactory.Create(chatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer),
            (reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem) => reviewVerifierAgentFactory.Create(chatClient, reviewRepositoryRootPath, reviewRuleKey, reviewRuleMarkdown, reviewTaskItem, issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            promptAssetReader,
            agentStatusEventPublisher: agentStatusEventPublisher);

        return workflow.RunAsync(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken);
    }

    private Task<Result<RuleReportWorkflowResult>> RunRuleReportWorkflowAsync(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        OperationalContextAgentCompactionOptions compactionOptions,
        IRuleReportIssueStore reportIssueStore,
        PromptAssetReader promptAssetReader,
        IAgentStatusEventPublisher agentStatusEventPublisher,
        CancellationToken cancellationToken)
    {
        ReviewVerdictBuffer verdictBuffer = new();
        ReportAggregatorAgentFactory reportAggregatorAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        ReportVerifierAgentFactory reportVerifierAgentFactory = new(compactionOptions, promptAssetReader, loggerFactory: _loggerFactory, serviceProvider: _serviceProvider);
        RuleReportWorkflow workflow = new(
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem) => reportAggregatorAgentFactory.Create(chatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, reportIssueStore, verdictBuffer),
            (reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues) => reportVerifierAgentFactory.Create(chatClient, reportRepositoryRootPath, reportRuleKey, reportRuleMarkdown, reportTaskItem, issues, reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            promptAssetReader,
            agentStatusEventPublisher: agentStatusEventPublisher);

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

    private sealed class AgentCompactionOptions
    {
        public required OperationalContextAgentCompactionOptions Scan { get; init; }

        public required OperationalContextAgentCompactionOptions ProjectPlan { get; init; }

        public required OperationalContextAgentCompactionOptions RuleReview { get; init; }

        public required OperationalContextAgentCompactionOptions Report { get; init; }
    }
}
