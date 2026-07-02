using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal interface IDependenciesFactory
{
    ReviewAgentTeamDependencies CreateDependencies(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
