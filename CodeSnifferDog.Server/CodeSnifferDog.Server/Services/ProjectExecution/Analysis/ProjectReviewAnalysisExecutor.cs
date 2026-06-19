using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Status;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal sealed class ProjectReviewAnalysisExecutor(
    IProjectChatClientProvider chatClientProvider,
    IProjectReviewAgentTeamWorkerFactory workerFactory,
    IAgentStatusEventSubscriberFactory agentStatusEventSubscriberFactory,
    IOptions<ProjectExecutionOptions> options) : IProjectReviewAnalysisExecutor
{
    private readonly IProjectChatClientProvider _chatClientProvider = chatClientProvider;
    private readonly IProjectReviewAgentTeamWorkerFactory _workerFactory = workerFactory;
    private readonly IAgentStatusEventSubscriberFactory _agentStatusEventSubscriberFactory = agentStatusEventSubscriberFactory;
    private readonly ExecutionOptions _options = options.Value.ExecutionOptions;

    public async Task<ReviewAgentTeamAnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        CancellationToken cancellationToken = default)
    {
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

            return await worker.AnalyzeDetailedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            eventStream.Complete();
        }
    }
}
