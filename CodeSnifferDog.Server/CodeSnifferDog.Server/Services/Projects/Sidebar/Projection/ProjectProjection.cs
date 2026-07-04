using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;

/// <summary>
/// Sidebar read model for one project row.
/// </summary>
/// <param name="ProjectId">Project identifier.</param>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="Status">Persisted processing status.</param>
/// <param name="CreatedAtUtc">Project creation timestamp in UTC.</param>
/// <param name="QueueTimestampUtc">Queue timestamp in UTC.</param>
/// <param name="FinishedAtUtc">Finish timestamp in UTC, when one exists.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
internal sealed record ProjectProjection(
    Guid ProjectId,
    string OriginalFileName,
    ProjectProcessingStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset UpdatedAtUtc);
