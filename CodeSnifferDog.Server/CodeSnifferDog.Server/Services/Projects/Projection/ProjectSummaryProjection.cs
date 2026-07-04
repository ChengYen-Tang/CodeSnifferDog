using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Read model used to project a full project summary.
/// </summary>
/// <param name="ProjectId">Project identifier.</param>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="Status">Persisted processing status.</param>
/// <param name="FileSizeBytes">Stored project size in bytes.</param>
/// <param name="CreatedAtUtc">Project creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
/// <param name="QueueTimestampUtc">Queue timestamp in UTC.</param>
/// <param name="ProcessingStartedAtUtc">Processing start timestamp in UTC, when one exists.</param>
/// <param name="FinishedAtUtc">Processing finish timestamp in UTC, when one exists.</param>
/// <param name="FailureReason">Failure reason, when the project failed.</param>
internal sealed record ProjectSummaryProjection(
    Guid ProjectId,
    string OriginalFileName,
    ProjectProcessingStatus Status,
    long FileSizeBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? ProcessingStartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? FailureReason);
