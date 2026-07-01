using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal sealed class ProjectReviewAnalysisExecutor(
    IProjectChatClientProvider chatClientProvider,
    IProjectReviewAgentTeamWorkerFactory workerFactory,
    IAgentStatusEventSubscriberFactory agentStatusEventSubscriberFactory,
    IOptions<ProjectExecutionOptions> options,
    ILogger<ProjectReviewAnalysisExecutor>? logger = null) : IProjectReviewAnalysisExecutor
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IProjectReviewAgentTeamWorkerFactory _workerFactory = workerFactory;
    private readonly IAgentStatusEventSubscriberFactory _agentStatusEventSubscriberFactory = agentStatusEventSubscriberFactory;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;
    private readonly ILogger<ProjectReviewAnalysisExecutor> _logger = logger ?? NullLogger<ProjectReviewAnalysisExecutor>.Instance;

    public async Task<ReviewAgentTeamAnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Project {ProjectId} agent team analysis started. Rule count: {RuleCount}; repository: {RepositoryRootPath}.",
            context.ProjectId,
            rules.Count,
            context.RepositoryRootPath);

        IChatClient chatClient = _chatClientProvider.CreateChatClient();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber eventSubscriber =
            _agentStatusEventSubscriberFactory.Create(context.ProjectId, eventStream.Events);

        try
        {
            await using IProjectReviewAgentTeamWorker worker = _workerFactory.CreateWorker(
                chatClient,
                context.RepositoryRootPath,
                rules,
                _options,
                eventStream);

            ReviewAgentTeamAnalysisResult result = await worker.AnalyzeDetailedAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Project {ProjectId} agent team analysis completed in {DurationMs} ms. Reports: {ReportCount}; errors: {ErrorCount}.",
                context.ProjectId,
                stopwatch.ElapsedMilliseconds,
                result.RuleReports.Count,
                result.ExecutionErrors.Count);
            return result;
        }
        finally
        {
            eventStream.Complete();
        }
    }
}
