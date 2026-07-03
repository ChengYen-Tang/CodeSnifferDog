namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class LiveCursorDto
{
    public required Guid AgentId { get; init; }

    public required long LatestSequence { get; init; }
}
