namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentStatusChangedDto
{
    public required Guid AgentId { get; init; }

    public required ProjectAgentRunStatus Status { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }
}
