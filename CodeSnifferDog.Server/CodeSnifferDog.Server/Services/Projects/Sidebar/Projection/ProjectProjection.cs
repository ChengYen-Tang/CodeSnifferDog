using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;

internal sealed record ProjectProjection(
    Guid ProjectId,
    string OriginalFileName,
    ProjectProcessingStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset UpdatedAtUtc);
