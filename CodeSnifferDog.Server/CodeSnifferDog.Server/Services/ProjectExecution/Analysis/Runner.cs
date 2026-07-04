using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Coordinates rule loading, agent-team execution, and completion for project analysis.
/// </summary>
internal sealed class Runner(
    IProjectChatClientProvider chatClientProvider,
    IReviewRuleMarkdownProvider ruleMarkdownProvider,
    IReviewAnalysisExecutor analysisExecutor,
    ICompletionService completionService,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IOptions<Settings> options,
    ILogger<Runner> logger) : IProjectAnalysisRunner
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IReviewRuleMarkdownProvider _ruleMarkdownProvider = ruleMarkdownProvider;
    private readonly IReviewAnalysisExecutor _analysisExecutor = analysisExecutor;
    private readonly ICompletionService _completionService = completionService;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;
    private readonly ILogger<Runner> _logger = logger;

    /// <inheritdoc />
    public bool IsReady => _chatClientProvider.IsReady && _ruleMarkdownProvider.HasRules;

    /// <inheritdoc />
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

        IReadOnlyList<RuleDefinition> rules = await _ruleMarkdownProvider
            .LoadRulesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rules.Count == 0)
            throw new InvalidOperationException("No review rule markdown files were found.");

        AnalysisResult analysisResult = await _analysisExecutor
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

    /// <summary>
    /// Validates execution options required by project analysis.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when any required execution option is invalid.</exception>
    private void ValidateOptions()
    {
        if (_options.MaxParallelAgents <= 0)
            throw new InvalidOperationException("ExecutionOptions:MaxParallelAgents must be greater than zero.");

        if (_options.ModelContextWindowTokens <= 0)
            throw new InvalidOperationException("ExecutionOptions:ModelContextWindowTokens must be greater than zero.");

        if (_options.AgentRunTimeoutSeconds <= 0)
            throw new InvalidOperationException("ExecutionOptions:AgentRunTimeoutSeconds must be greater than zero.");

        if (_options.MaxConsecutiveAgentRunFailures < 0)
            throw new InvalidOperationException("ExecutionOptions:MaxConsecutiveAgentRunFailures must be zero or greater.");

        if (_options.MaxMissingSubmissionAttempts < 0)
            throw new InvalidOperationException("ExecutionOptions:MaxMissingSubmissionAttempts must be zero or greater.");

        if (_options.MaxVerifierRejectionAttempts < 0)
            throw new InvalidOperationException("ExecutionOptions:MaxVerifierRejectionAttempts must be zero or greater.");

    }

    /// <summary>
    /// Clears persisted agent status data before a new analysis run starts.
    /// </summary>
    /// <param name="projectId">Project identifier whose status data should be cleared.</param>
    /// <param name="cancellationToken">Token that cancels the database operation.</param>
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
