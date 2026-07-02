using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal interface IWorkerFactory
{
    IWorker CreateWorker(
        IChatClient chatClient,
        string repositoryRootPath,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
