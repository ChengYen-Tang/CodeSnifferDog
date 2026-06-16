using Microsoft.Agents.AI;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed class AgentCreationResult
{
    public required AIAgent Agent { get; init; }

    public required string SystemPrompt { get; init; }
}
