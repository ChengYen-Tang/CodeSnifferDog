using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectReviewAnalysisExecutor(
    IProjectChatClientProvider chatClientProvider,
    IProjectReviewAgentTeamWorkerFactory workerFactory,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectAgentStatusLiveUpdateNotifier projectAgentStatusLiveUpdateNotifier,
    IOptions<ProjectExecutionOptions> options) : IProjectReviewAnalysisExecutor
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IProjectReviewAgentTeamWorkerFactory _workerFactory = workerFactory;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _projectAgentStatusLiveUpdateNotifier = projectAgentStatusLiveUpdateNotifier;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;

    public async Task<ReviewAgentTeamAnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        CancellationToken cancellationToken = default)
    {
        IChatClient chatClient = _chatClientProvider.CreateChatClient();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber eventSubscriber =
            new(context.ProjectId, _dbContextFactory, _projectAgentStatusLiveUpdateNotifier, eventStream.Events);

        try
        {
            await using IProjectReviewAgentTeamWorker worker = _workerFactory.CreateWorker(
                chatClient,
                context.RepositoryRootPath,
                rules,
                _options,
                eventStream);

            return await worker.AnalyzeDetailedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            eventStream.Complete();
        }
    }
}
