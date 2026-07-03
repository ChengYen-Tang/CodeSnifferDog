namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class LiveSubscriptionRequestDto
{
    public required Guid ProjectId { get; init; }

    public required DateTimeOffset SnapshotGeneratedAtUtc { get; init; }

    public Guid? AgentId { get; init; }

    public long LatestSequence { get; init; }
}
