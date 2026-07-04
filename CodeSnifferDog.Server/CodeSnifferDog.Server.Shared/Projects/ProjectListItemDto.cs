namespace CodeSnifferDog.Server.Shared.Projects;

/// <summary>
/// Represents a project in a flat project list.
/// </summary>
public sealed class ProjectListItemDto
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the original uploaded file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the current project status.
    /// </summary>
    public required ProjectStatus Status { get; init; }

    /// <summary>
    /// Gets when the project was created.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
