namespace CodeSnifferDog.Server.Shared.Projects;

/// <summary>
/// Represents a sidebar group that contains related projects.
/// </summary>
public sealed class ProjectSidebarGroupDto
{
    /// <summary>
    /// Gets the stable group key.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the user-facing group name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the status used to order or style the group.
    /// </summary>
    public required ProjectStatus Status { get; init; }

    /// <summary>
    /// Gets the sort order of the group.
    /// </summary>
    public required int SortOrder { get; init; }

    /// <summary>
    /// Gets the projects that belong to the group.
    /// </summary>
    public IReadOnlyList<ProjectSidebarProjectDto> Projects { get; init; } = [];
}
