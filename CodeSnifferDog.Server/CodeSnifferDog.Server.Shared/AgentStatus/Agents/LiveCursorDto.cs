namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Tracks the latest timeline position known to a client for an agent.
/// </summary>
public sealed class LiveCursorDto
{
    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// Gets the latest timeline sequence received by the client.
    /// </summary>
    public required long LatestSequence { get; init; }
}
