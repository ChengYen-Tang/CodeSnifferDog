using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

public sealed class ProjectSummaryDto
{
    public required Guid ProjectId { get; init; }

    public required string OriginalFileName { get; init; }

    public required ProjectProcessingStatus Status { get; init; }

    public long FileSizeBytes { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public required DateTimeOffset QueueTimestampUtc { get; init; }

    public DateTimeOffset? ProcessingStartedAtUtc { get; init; }

    public DateTimeOffset? FinishedAtUtc { get; init; }

    public string? FailureReason { get; init; }
}
