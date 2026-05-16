namespace CodeSnifferDog.Server.Shared.Projects;

public sealed class ProjectSidebarProjectDto
{
    public required Guid ProjectId { get; init; }

    public required string OriginalFileName { get; init; }

    public required ProjectStatus Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required int SortOrder { get; init; }
}
