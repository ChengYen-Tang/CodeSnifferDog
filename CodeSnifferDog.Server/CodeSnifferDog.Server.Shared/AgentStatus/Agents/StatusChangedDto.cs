namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class StatusChangedDto
{
    public required Guid AgentId { get; init; }

    public required RunStatus Status { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }
}
