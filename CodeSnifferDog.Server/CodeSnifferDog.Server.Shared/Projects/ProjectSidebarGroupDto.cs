namespace CodeSnifferDog.Server.Shared.Projects;

public sealed class ProjectSidebarGroupDto
{
    public required string GroupKey { get; init; }

    public required string DisplayName { get; init; }

    public required ProjectStatus Status { get; init; }

    public required int SortOrder { get; init; }

    public IReadOnlyList<ProjectSidebarProjectDto> Projects { get; init; } = [];
}
