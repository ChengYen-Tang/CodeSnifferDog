using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentStatusSnapshotDto
{
    public required Guid ProjectId { get; init; }

    public required ProjectStatus ProjectStatus { get; init; }

    public required DateTimeOffset SnapshotGeneratedAtUtc { get; init; }

    public required IReadOnlyList<ProjectAgentGroupSnapshotDto> AgentGroups { get; init; }
}
