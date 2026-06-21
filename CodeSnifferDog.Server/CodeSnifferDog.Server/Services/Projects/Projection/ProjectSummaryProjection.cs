using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

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
