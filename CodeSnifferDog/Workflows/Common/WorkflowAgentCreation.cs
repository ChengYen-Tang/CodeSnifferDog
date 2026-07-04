using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;

namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Converts agent-factory exceptions into <see cref="Result" /> failures.
/// </summary>
internal static class WorkflowAgentCreation
{
    /// <summary>
    /// Tries to create one agent result and captures thrown exceptions as failures.
    /// </summary>
    /// <param name="factory">Factory that creates the agent result.</param>
    /// <param name="agentName">Logical agent name used in failure messages.</param>
    /// <returns>A successful result when creation succeeds; otherwise a failure containing the thrown exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="agentName" /> is null, empty, or whitespace.</exception>
    public static Result<AgentCreationResult> TryCreate(Func<AgentCreationResult> factory, string agentName)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        try
        {
            return Result.Ok(factory());
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError($"Failed to create {agentName}: {ex}", ex));
        }
    }
}
