using Microsoft.Agents.AI;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

/// <summary>
/// Holds a created agent instance together with the system prompt used to build it.
/// </summary>
public sealed class AgentCreationResult
{
    /// <summary>
    /// Gets the created agent instance.
    /// </summary>
    public required AIAgent Agent { get; init; }

    /// <summary>
    /// Gets the system prompt assigned to the created agent.
    /// </summary>
    public required string SystemPrompt { get; init; }
}
