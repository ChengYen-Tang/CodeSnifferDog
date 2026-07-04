using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

/// <summary>
/// Creates the runtime dependencies required by the review-team worker.
/// </summary>
internal interface IDependenciesFactory
{
    /// <summary>
    /// Creates the dependency graph required by the review-team runtime.
    /// </summary>
    /// <param name="chatClient">Chat client used by the runtime.</param>
    /// <param name="executionOptions">Execution options applied to the runtime.</param>
    /// <param name="agentEventBus">Event bus that receives runtime events.</param>
    /// <returns>The created dependencies.</returns>
    Dependencies CreateDependencies(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus);
}
