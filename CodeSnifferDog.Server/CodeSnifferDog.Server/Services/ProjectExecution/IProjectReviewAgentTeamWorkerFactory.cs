using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal interface IProjectReviewAgentTeamWorkerFactory
{
    IProjectReviewAgentTeamWorker CreateWorker(
        IChatClient chatClient,
        string repositoryRootPath,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
