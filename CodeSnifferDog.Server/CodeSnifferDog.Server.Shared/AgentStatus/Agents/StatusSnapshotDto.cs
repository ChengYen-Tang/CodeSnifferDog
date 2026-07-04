using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Carries the full agent-status snapshot for a project.
/// </summary>
public sealed class StatusSnapshotDto
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the current project status.
    /// </summary>
    public required ProjectStatus ProjectStatus { get; init; }

    /// <summary>
    /// Gets when the snapshot was generated.
    /// </summary>
    public required DateTimeOffset SnapshotGeneratedAtUtc { get; init; }

    /// <summary>
    /// Gets the persisted agent groups included in the snapshot.
    /// </summary>
    public required IReadOnlyList<GroupSnapshotDto> AgentGroups { get; init; }
}
