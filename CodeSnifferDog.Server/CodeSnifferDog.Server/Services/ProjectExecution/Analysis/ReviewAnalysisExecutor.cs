using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal sealed class ReviewAnalysisExecutor(
    IProjectChatClientProvider chatClientProvider,
    IWorkerFactory workerFactory,
    IEventSubscriberFactory agentStatusEventSubscriberFactory,
    IOptions<Settings> options,
    ILogger<ReviewAnalysisExecutor>? logger = null) : IReviewAnalysisExecutor
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IWorkerFactory _workerFactory = workerFactory;
    private readonly IEventSubscriberFactory _agentStatusEventSubscriberFactory = agentStatusEventSubscriberFactory;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;
    private readonly ILogger<ReviewAnalysisExecutor> _logger = logger ?? NullLogger<ReviewAnalysisExecutor>.Instance;

    public async Task<AnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<RuleDefinition> rules,
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
        await using EventSubscriber eventSubscriber =
            _agentStatusEventSubscriberFactory.Create(context.ProjectId, eventStream.Events);

        try
        {
            await using IWorker worker = _workerFactory.CreateWorker(
                chatClient,
                context.RepositoryRootPath,
                rules,
                _options,
                eventStream);

            AnalysisResult result = await worker.AnalyzeDetailedAsync(cancellationToken).ConfigureAwait(false);
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
