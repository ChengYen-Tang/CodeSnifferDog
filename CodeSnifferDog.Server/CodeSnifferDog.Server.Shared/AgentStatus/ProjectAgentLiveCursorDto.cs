namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentLiveCursorDto
{
    public required Guid AgentId { get; init; }

    public required long LatestSequence { get; init; }
}
