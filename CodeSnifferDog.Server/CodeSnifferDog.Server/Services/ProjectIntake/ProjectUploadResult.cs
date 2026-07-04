using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

/// <summary>
/// Result returned immediately after a project upload is queued.
/// </summary>
public sealed class ProjectUploadResult
{
    /// <summary>
    /// Gets the queued project identifier.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the original uploaded file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the shared project status after queueing.
    /// </summary>
    public required ProjectStatus Status { get; init; }

    /// <summary>
    /// Gets the uploaded file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets when the project record was created, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets when the project entered the queue, in UTC.
    /// </summary>
    public required DateTimeOffset QueueTimestampUtc { get; init; }
}
