using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal interface IProjectReviewAgentTeamDependenciesFactory
{
    ReviewAgentTeamDependencies CreateDependencies(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
