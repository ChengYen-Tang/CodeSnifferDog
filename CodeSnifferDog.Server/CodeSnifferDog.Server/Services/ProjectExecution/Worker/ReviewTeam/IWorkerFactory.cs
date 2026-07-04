using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

/// <summary>
/// Creates review-team workers for project analysis.
/// </summary>
internal interface IWorkerFactory
{
    /// <summary>
    /// Creates a review-team worker for a repository and rule set.
    /// </summary>
    /// <param name="chatClient">Chat client used by the review-team runtime.</param>
    /// <param name="repositoryRootPath">Repository root path to analyze.</param>
    /// <param name="rules">Rules that should be evaluated.</param>
    /// <param name="executionOptions">Execution options applied to the worker.</param>
    /// <param name="agentEventBus">Event bus that receives worker events.</param>
    /// <returns>The created worker.</returns>
    IWorker CreateWorker(
        IChatClient chatClient,
        string repositoryRootPath,
        IReadOnlyList<RuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
