using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class StatusSnapshotDto
{
    public required Guid ProjectId { get; init; }

    public required ProjectStatus ProjectStatus { get; init; }

    public required DateTimeOffset SnapshotGeneratedAtUtc { get; init; }

    public required IReadOnlyList<GroupSnapshotDto> AgentGroups { get; init; }
}
