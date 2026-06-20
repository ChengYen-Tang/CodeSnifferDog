using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal sealed record ProjectSidebarProjectProjection(
    Guid ProjectId,
    string OriginalFileName,
    ProjectProcessingStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset UpdatedAtUtc);
