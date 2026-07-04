namespace CodeSnifferDog.Server.Shared.Projects;

/// <summary>
/// Carries the complete sidebar snapshot for the project list UI.
/// </summary>
public sealed class ProjectSidebarSnapshotDto
{
    /// <summary>
    /// Gets when the snapshot was generated.
    /// </summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>
    /// Gets the currently selected project identifier, if any.
    /// </summary>
    public Guid? SelectedProjectId { get; init; }

    /// <summary>
    /// Gets the grouped sidebar projects.
    /// </summary>
    public IReadOnlyList<ProjectSidebarGroupDto> Groups { get; init; } = [];
}
