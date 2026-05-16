namespace CodeSnifferDog.Server.Shared.Projects;

public sealed class ProjectSidebarSnapshotDto
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public Guid? SelectedProjectId { get; init; }

    public IReadOnlyList<ProjectSidebarGroupDto> Groups { get; init; } = [];
}
