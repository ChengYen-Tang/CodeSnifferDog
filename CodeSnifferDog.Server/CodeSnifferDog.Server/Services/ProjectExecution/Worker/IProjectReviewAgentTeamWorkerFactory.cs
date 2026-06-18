using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker;

internal interface IProjectReviewAgentTeamWorkerFactory
{
    IProjectReviewAgentTeamWorker CreateWorker(
        IChatClient chatClient,
        string repositoryRootPath,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
