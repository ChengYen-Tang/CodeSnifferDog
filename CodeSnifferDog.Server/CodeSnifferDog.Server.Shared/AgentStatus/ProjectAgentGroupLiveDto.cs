namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentGroupLiveDto
{
    public required Guid GroupId { get; init; }

    public required string RuntimeKey { get; init; }

    public required string DisplayName { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}
