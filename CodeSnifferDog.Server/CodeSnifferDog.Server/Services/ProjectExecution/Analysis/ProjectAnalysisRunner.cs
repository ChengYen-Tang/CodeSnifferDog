using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal sealed class ProjectAnalysisRunner(
    IProjectChatClientProvider chatClientProvider,
    IReviewRuleMarkdownProvider ruleMarkdownProvider,
    IProjectReviewAnalysisExecutor analysisExecutor,
    IProjectAnalysisCompletionService completionService,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IOptions<ProjectExecutionOptions> options,
    ILogger<ProjectAnalysisRunner> logger) : IProjectAnalysisRunner
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IReviewRuleMarkdownProvider _ruleMarkdownProvider = ruleMarkdownProvider;
    private readonly IProjectReviewAnalysisExecutor _analysisExecutor = analysisExecutor;
    private readonly IProjectAnalysisCompletionService _completionService = completionService;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;
    private readonly ILogger<ProjectAnalysisRunner> _logger = logger;

    public bool IsReady => _chatClientProvider.IsReady && _ruleMarkdownProvider.HasRules;

    public async Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (!IsReady)
            throw new InvalidOperationException("Project analysis runner is not ready.");

        ValidateOptions();

        _logger.LogInformation(
            "Project {ProjectId} analysis started for repository {RepositoryRootPath}.",
            context.ProjectId,
            context.RepositoryRootPath);

        await ClearAgentStatusDataAsync(context.ProjectId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ProjectExecutionRuleDefinition> rules = await _ruleMarkdownProvider
            .LoadRulesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rules.Count == 0)
            throw new InvalidOperationException("No review rule markdown files were found.");

        ReviewAgentTeamAnalysisResult analysisResult = await _analysisExecutor
            .AnalyzeAsync(context, rules, cancellationToken)
            .ConfigureAwait(false);

        await _completionService
            .CompleteAnalysisAsync(context.ProjectId, rules, analysisResult, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Project {ProjectId} analysis completed in {DurationMs} ms. Rule count: {RuleCount}; report count: {ReportCount}; error count: {ErrorCount}; has findings: {HasAnyFindings}.",
            context.ProjectId,
            stopwatch.ElapsedMilliseconds,
            rules.Count,
            analysisResult.RuleReports.Count,
            analysisResult.ExecutionErrors.Count,
            analysisResult.HasAnyFindings);
    }

    private void ValidateOptions()
    {
        if (_options.MaxParallelAgents <= 0)
            throw new InvalidOperationException("ExecutionOptions:MaxParallelAgents must be greater than zero.");

        if (_options.ModelContextWindowTokens <= 0)
            throw new InvalidOperationException("ExecutionOptions:ModelContextWindowTokens must be greater than zero.");

        if (_options.AgentRunTimeoutSeconds <= 0)
            throw new InvalidOperationException("ExecutionOptions:AgentRunTimeoutSeconds must be greater than zero.");

        if (_options.MaxConsecutiveAgentRunFailures <= 0)
            throw new InvalidOperationException("ExecutionOptions:MaxConsecutiveAgentRunFailures must be greater than zero.");
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
}
