using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

/// <summary>
/// Detailed summary of one uploaded project.
/// </summary>
public sealed class ProjectSummaryDto
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
    /// Gets the shared project status.
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
    /// Gets when the project record was last updated, in UTC.
    /// </summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets when the project entered the queue, in UTC.
    /// </summary>
    public required DateTimeOffset QueueTimestampUtc { get; init; }

    /// <summary>
    /// Gets when processing started, in UTC, when one exists.
    /// </summary>
    public DateTimeOffset? ProcessingStartedAtUtc { get; init; }

    /// <summary>
    /// Gets when processing finished, in UTC, when one exists.
    /// </summary>
    public DateTimeOffset? FinishedAtUtc { get; init; }

    /// <summary>
    /// Gets the failure reason when processing failed.
    /// </summary>
    public string? FailureReason { get; init; }
}
