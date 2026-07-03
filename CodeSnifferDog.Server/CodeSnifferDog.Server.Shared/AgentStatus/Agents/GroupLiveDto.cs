namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class GroupLiveDto
{
    public required Guid GroupId { get; init; }

    public required string RuntimeKey { get; init; }

    public required string DisplayName { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}
