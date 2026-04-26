namespace CodeSnifferDog.Server.Shared.Projects;

public sealed class ProjectListItemDto
{
    public required Guid ProjectId { get; init; }

    public required string OriginalFileName { get; init; }

    public required ProjectStatus Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}
